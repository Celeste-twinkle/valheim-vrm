using System;
using System.Linq;
using ValheimVRM.Sync;

static class Program
{
    static string Hash(char c) => new string(c,64);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        var orderA = new AvatarRequestOrder(); var orderB = new AvatarRequestOrder();
        Check(orderA.TryAccept(0), "Legacy connection no longer accepted");
        Check(orderA.TryAccept(2) && !orderA.TryAccept(1), "Late older request replaced newer request");
        Check(!orderA.TryAccept(2) && !orderA.TryAccept(0) && !orderA.TryAccept(-1), "Duplicate, downgrade or negative request accepted");
        Check(orderB.TryAccept(1) && orderA.TryAccept(3), "Player request counters are not independent");
        Check(new AvatarRequestOrder().TryAccept(1), "New connection retained a previous watermark");
        Check(orderA.TryAccept(long.MaxValue) && !orderA.TryAccept(long.MinValue), "Sequence overflow was accepted");
        var server = new AvatarSyncRegistry();
        Check(server.Set(101,1001,1,"模型 1",Hash('a')), "A selects model 1");
        Check(server.Set(202,2002,2,"模型 2",Hash('b')), "B selects model 2");
        Check(server.Snapshot().Single(s=>s.Peer==101).Model=="模型 1", "A was changed by B");
        var b=server.Snapshot().Single(s=>s.Peer==202);
        for(int i=0;i<100;i++) server.Set(101,1001,1,"模型 "+i,Hash('a'));
        Check(server.Snapshot().Single(s=>s.Peer==202).SameAs(b), "A's rapid switches changed B");
        Check(!server.Set(101,2002,2,"Impersonation",Hash('c')), "A claimed B's character");
        Check(server.Snapshot().Single(s=>s.Peer==202).SameAs(b), "Rejected claim changed B");
        Check(server.Set(101,1001,99,"重生",Hash('a')), "Respawn did not update character ID");
        Check(server.Snapshot().All(s=>s.Peer!=101 || s.CharacterId==99), "Old incarnation retained");
        var lateJoin=server.Snapshot();
        Check(lateJoin.Length==2 && lateJoin.Single(s=>s.Peer==101).Model=="重生", "Late join lost current selection");
        Check(server.Remove(101), "Disconnect did not remove A");
        Check(server.Snapshot().Length==1 && server.Snapshot()[0].SameAs(b), "Disconnect changed B");
        Check(server.Set(303,3003,1,"重连",Hash('a')), "New connection cannot select");
        Check(server.Remove(303) && server.Snapshot().Single().SameAs(b), "Local opt-out changed B");
        foreach(string invalid in new[]{"", "..", "../model", "folder/model", "C:\\model", "x:stream", "x\n", new string('x',256)})
            Check(!server.Set(101,1001,1,invalid,Hash('a')), "Unsafe model accepted: "+invalid);
        Check(!server.Set(101,1001,1,"Valid","bad"), "Bad fingerprint accepted");
        Check(!server.Set(0,1001,1,"Valid",Hash('a')), "Anonymous connection accepted");
        Check(!server.Set(101,0,0,"Valid",Hash('a')), "Missing character accepted");
        Check(new AvatarSyncRegistry().Snapshot().Length==0, "New server session leaked prior selections");
        // Equal model names still represent separate players and separate instances.
        Check(server.Set(101,1001,2,b.Model,b.Sha256), "Shared asset cannot be selected twice");
        Check(server.Snapshot().Length==2, "Shared asset merged players");
        Check(server.Set(101,1001,2,b.Model,b.Sha256,1.4f), "Height-only change was ignored");
        Check(server.Snapshot().Single(s=>s.Peer==101).Height==1.4f && server.Snapshot().Single(s=>s.Peer==202).Height==2f,
            "Shared avatar height changed the other player");
        long heightRevision=server.Revision;
        foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,1.39f,2.21f})
            Check(!server.Set(101,1001,2,b.Model,b.Sha256,value), "Invalid height accepted");
        Check(server.Revision==heightRevision, "Invalid height changed revision");
        Check(server.Set(101,1001,2,b.Model,b.Sha256,2.2f), "Maximum height rejected");
        Check(!server.Set(101,1001,2,b.Model,b.Sha256,2.2f), "Unchanged height advanced revision");
        var controls = new AvatarCalibrationData { Standing=.1f, Sitting=-.2f, Physics=.75f };
        controls.Animations["Base Layer.站姿"] = new AvatarCalibrationData.Position(.1f,.2f,-.3f);
        string encoded = AvatarCalibrationCodec.Encode(controls);
        Check(AvatarCalibrationCodec.TryDecode(encoded,out var decoded) && AvatarCalibrationCodec.Encode(decoded)==encoded,"Calibration roundtrip failed");
        Check(server.Set(101,1001,2,b.Model,b.Sha256,2.2f,encoded),"Calibration-only change ignored");
        long calibrationRevision=server.Revision;
        Check(!server.Set(101,1001,2,b.Model,b.Sha256,2.2f,encoded) && server.Revision==calibrationRevision,"Identical calibration advanced revision");
        Check(!server.Set(101,1001,2,b.Model,b.Sha256,2.2f,"broken") && server.Revision==calibrationRevision,"Invalid calibration replaced valid state");
        foreach(var invalid in new[]{null,"",encoded+" ",encoded.Substring(0,encoded.Length-4),new string('a',AvatarCalibrationCodec.MaxEncodedLength+1)})
            Check(!AvatarCalibrationCodec.TryDecode(invalid,out _),"Malformed calibration accepted");
        var bytes=Convert.FromBase64String(encoded);
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,1.01f,-1.01f})
        {
            Array.Copy(BitConverter.GetBytes(invalid),0,bytes,1,4);
            Check(!AvatarCalibrationCodec.TryDecode(Convert.ToBase64String(bytes),out _),"Nonfinite/out-of-range offset accepted");
        }
        foreach(Action<AvatarCalibrationData> corrupt in new Action<AvatarCalibrationData>[]{
            d=>d.Physics=1.1f, d=>d.Left.Scale=.2f, d=>d.Right.Offset=new AvatarCalibrationData.Position(0,1.01f,0),
            d=>d.Animations["\n"]=default(AvatarCalibrationData.Position),
            d=>d.Animations["Base Layer.Movement"]=new AvatarCalibrationData.Position(0, 1.01f, 0),
            d=>{for(int i=0;i<=AvatarCalibrationCodec.MaxEntries;i++)d.Animations["state"+i]=default(AvatarCalibrationData.Position);}
        })
        {
            bool rejected=false; var data=new AvatarCalibrationData(); corrupt(data);
            try { AvatarCalibrationCodec.Encode(data); } catch(ArgumentException) { rejected=true; }
            Check(rejected,"Invalid owner calibration encoded");
        }
        Check(server.Snapshot().Single(s=>s.Peer==202).SameAs(b),"Calibration changed another player's state");
        controls.Back = new AvatarCalibrationData.Item { Scale=1.4f, Offset=new AvatarCalibrationData.Position(.1f,-.2f,.3f) };
        Check(AvatarCalibrationCodec.TryDecode(AvatarCalibrationCodec.Encode(controls),out var back) &&
            back.Back.Scale==1.4f && back.Back.Offset.Y==-.2f,"Back controls failed additive roundtrip");
        Check(AvatarCalibrationCodec.TryDecode(AvatarCalibrationCodec.Default,out var defaults) && defaults.Back.Scale==1 &&
            defaults.Back.Offset.X==0,"Older payload did not use neutral back controls");
        controls.Back = new AvatarCalibrationData.Item { Scale=2.1f };
        bool backRejected=false; try{AvatarCalibrationCodec.Encode(controls);}catch(ArgumentException){backRejected=true;}
        Check(backRejected,"Out-of-range back scale accepted");
        foreach(float offset in new[]{-1f,-.75f,.75f,1f})
        {
            var wide=new AvatarCalibrationData { Standing=offset, Sitting=-offset };
            foreach(var item in new[]{wide.Left,wide.Right,wide.TwoHanded})item.Offset=new AvatarCalibrationData.Position(offset,-offset,offset);
            wide.Back=new AvatarCalibrationData.Item { Offset=new AvatarCalibrationData.Position(-offset,offset,-offset) };
            wide.Animations["Base Layer.Movement"]=new AvatarCalibrationData.Position(offset,offset,-offset);
            string state=AvatarCalibrationCodec.Encode(wide);
            Check(AvatarCalibrationCodec.TryDecode(state,out var roundtrip) && AvatarCalibrationCodec.Encode(roundtrip)==state,"100 cm calibration was lost");
            Check(server.Set(101,1001,2,b.Model,b.Sha256,2f,state) && server.Snapshot().Single(s=>s.Peer==101).Calibration==state,"Server rejected extended offsets");
            Check(server.Snapshot().Single(s=>s.Peer==202).SameAs(b),"Extended offsets changed player B");
        }
        foreach(float invalid in new[]{-1.01f,1.01f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        foreach(Action<AvatarCalibrationData,float> set in new Action<AvatarCalibrationData,float>[] {
            (d,v)=>d.Standing=v,(d,v)=>d.Sitting=v,(d,v)=>d.Left.Offset.X=v,(d,v)=>d.Right.Offset.Y=v,
            (d,v)=>d.TwoHanded.Offset.Z=v,(d,v)=>d.Back=new AvatarCalibrationData.Item { Offset=new AvatarCalibrationData.Position(v,0,0) },
            (d,v)=>d.Animations["Base Layer.Movement"]=new AvatarCalibrationData.Position(0,0,v) })
        {
            var bad=new AvatarCalibrationData();set(bad,invalid);bool rejected=false;
            try{AvatarCalibrationCodec.Encode(bad);}catch(ArgumentException){rejected=true;}
            Check(rejected,"Invalid extended calibration escaped validation");
        }
        Console.WriteLine("PASS: +/-75 and +/-100 cm across every offset group; beyond +/-100 cm and nonfinite values rejected; per-player relay isolation preserved.");
        Console.WriteLine("PASS: canonical calibration roundtrip, malformed/nonfinite/range/entry limits, revision deduplication and per-player isolation.");
        Console.WriteLine("PASS: request ordering, duplicate/downgrade/overflow rejection, independent player identities, rapid switches, respawn, reconnect, late join, opt-out and path/hash rejection.");
    }
}
