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
        Console.WriteLine("PASS: request ordering, duplicate/downgrade/overflow rejection, independent player identities, rapid switches, respawn, reconnect, late join, opt-out and path/hash rejection.");
    }
}
