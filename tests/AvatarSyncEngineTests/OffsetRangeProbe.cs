using System;
using System.IO;
using UnityEngine;
using ValheimVRM;

static class OffsetRangeProbe
{
    public static void Run(string output)
    {
        foreach(float v in new[]{-1f,-.75f,0f,.75f,1f})
            Check(AvatarHeightOffsets.Clamp(v)==v,"Valid offset was clamped");
        Check(AvatarHeightOffsets.Clamp(-2)==-1 && AvatarHeightOffsets.Clamp(2)==1,"Wrong clamp endpoints");
        foreach(float v in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Check(AvatarHeightOffsets.Clamp(v)==0,"Nonfinite local offset accepted");
        var options=new AvatarCalibrationOptions(Path.Combine(output,"preferences"));
        foreach(float v in new[]{-1f,-.75f,.75f,1f})
        {
            var profile=options.Get("Range fixture");var offset=new Vector3(v,-v,v);
            profile.Set("Base Layer.Range",offset);
            foreach(var item in new[]{profile.Left,profile.Right,profile.TwoHanded,profile.Back})item.Position.Value=offset;
            options.Changed();options.Save();options.Load();profile=options.Get("Range fixture");
            Check(profile.Get("Base Layer.Range")==offset,"Saved animation offset lost");
            foreach(var item in new[]{profile.Left,profile.Right,profile.TwoHanded,profile.Back})Check(item.Position.Value==offset,"Saved equipment offset lost");
        }
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
