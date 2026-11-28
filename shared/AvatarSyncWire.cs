using System;
using System.Collections.Generic;

namespace ValheimVRM.Sync
{
    public static class AvatarSyncWire
    {
        public const string Hello = "ValheimVRM.Sync.Hello";
        public const string SequencedHello = "ValheimVRM.Sync.SequencedHello";
        public const string HeightHello = "ValheimVRM.Sync.HeightHello";
        public const string CalibrationHello = "ValheimVRM.Sync.CalibrationHello";
        public const string StateChunk = "ValheimVRM.Sync.StateChunk";
        const int HeightSnapshotVersion = 2;
        const int CalibrationSnapshotVersion = 3;
        public const int MaxSnapshotBytes = 24 * 1024 * 1024;
        public const string Select = "ValheimVRM.Sync.Select";
        public const string State = "ValheimVRM.Sync.State";
        public const string ServerGuid = "com.celestetwinkle.valheimvrm.server";

        public static ZPackage Selection(bool enabled, string model, string hash)
        {
            var p = new ZPackage(); p.Write(AvatarSyncRules.Version); p.Write(enabled);
            p.Write(model ?? ""); p.Write(hash ?? ""); return p;
        }
        // Negotiated extension of the legacy packet. Old servers reject trailing
        // bytes, so clients append this only after SequencedHello.
        public static ZPackage Selection(bool enabled, string model, string hash, long sequence)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            var p = Selection(enabled, model, hash); p.Write(sequence); return p;
        }
        public static ZPackage Selection(bool enabled, string model, string hash, long sequence, float height)
        {
            if (!AvatarHeightRules.Valid(height)) throw new ArgumentOutOfRangeException(nameof(height));
            var p = Selection(enabled, model, hash, sequence); p.Write(height); return p;
        }
        public static ZPackage Selection(bool enabled, string model, string hash, long sequence, float height, string calibration)
        {
            if (!AvatarCalibrationCodec.TryDecode(calibration, out _)) throw new ArgumentException("Invalid calibration.");
            var p = Selection(enabled, model, hash, sequence, height); p.Write(calibration); return p;
        }
        public static bool ReadSelection(ZPackage p, out bool enabled, out string model, out string hash)
        {
            return ReadSelection(p, out enabled, out model, out hash, out var sequence) && sequence == 0;
        }
        public static bool ReadSelection(ZPackage p, out bool enabled, out string model, out string hash, out long sequence)
        {
            return ReadSelection(p, out enabled, out model, out hash, out sequence, out _, out var withHeight) && !withHeight;
        }
        public static bool ReadSelection(ZPackage p, out bool enabled, out string model, out string hash, out long sequence, out float height, out bool withHeight)
        {
            return ReadSelection(p, out enabled, out model, out hash, out sequence, out height, out withHeight, out var calibration) && calibration == null;
        }
        public static bool ReadSelection(ZPackage p, out bool enabled, out string model, out string hash, out long sequence, out float height, out bool withHeight, out string calibration)
        {
            enabled = false; model = hash = ""; sequence = 0;
            height = AvatarHeightRules.Default; withHeight = false; calibration = null;
            try
            {
                if (p == null || p.Size() > AvatarCalibrationCodec.MaxEncodedLength + 2048 || p.ReadInt() != AvatarSyncRules.Version) return false;
                enabled = p.ReadBool(); model = p.ReadString(); hash = p.ReadString();
                if (p.GetPos() != p.Size())
                {
                    int remaining = p.Size() - p.GetPos();
                    if (remaining < sizeof(long)) return false;
                    sequence = p.ReadLong();
                    if (sequence <= 0) return false;
                    if (remaining > sizeof(long))
                    {
                        if (remaining < sizeof(long) + sizeof(float)) return false;
                        height = p.ReadSingle(); withHeight = true;
                        if (!AvatarHeightRules.Valid(height)) return false;
                        if (p.GetPos() != p.Size())
                        {
                            calibration = p.ReadString();
                            if (!AvatarCalibrationCodec.TryDecode(calibration, out _)) return false;
                        }
                    }
                }
                return p.GetPos() == p.Size() && (model == "" && hash == "" ||
                    AvatarSyncRules.ValidModel(model) && AvatarSyncRules.ValidHash(hash));
            }
            catch (Exception) { return false; }
        }
        public static ZPackage Snapshot(long revision, AvatarSelection[] states, bool withHeight = false, bool withCalibration = false)
        {
            withHeight |= withCalibration;
            var p = new ZPackage(); p.Write(withCalibration ? CalibrationSnapshotVersion : withHeight ? HeightSnapshotVersion : AvatarSyncRules.Version); p.Write(revision); p.Write(states.Length);
            foreach (var s in states)
            {
                p.Write(s.Peer); p.Write(s.CharacterUser); p.Write(s.CharacterId); p.Write(s.Model); p.Write(s.Sha256);
                if (withHeight) p.Write(s.Height);
                if (withCalibration) p.Write(s.Calibration ?? AvatarCalibrationCodec.Default);
            }
            return p;
        }
        public static bool ReadSnapshot(ZPackage p, out long revision, out AvatarSelection[] states)
        {
            return ReadSnapshot(p, out revision, out states, out _);
        }
        public static bool ReadSnapshot(ZPackage p, out long revision, out AvatarSelection[] states, out bool withHeight)
        {
            return ReadSnapshot(p, out revision, out states, out withHeight, out _);
        }
        public static bool ReadSnapshot(ZPackage p, out long revision, out AvatarSelection[] states, out bool withHeight, out bool withCalibration)
        {
            revision = -1; states = null; withHeight = withCalibration = false;
            try
            {
                if (p == null || p.Size() > MaxSnapshotBytes) return false;
                int version = p.ReadInt();
                if (version != AvatarSyncRules.Version && version != HeightSnapshotVersion && version != CalibrationSnapshotVersion) return false;
                withCalibration = version == CalibrationSnapshotVersion;
                withHeight = version == HeightSnapshotVersion || withCalibration;
                revision = p.ReadLong(); int count = p.ReadInt();
                if (revision < 0 || count < 0 || count > AvatarSyncRules.MaxPlayers) return false;
                var peers = new HashSet<long>(); var characters = new HashSet<string>();
                var result = new AvatarSelection[count];
                for (int i = 0; i < count; i++)
                {
                    var s = new AvatarSelection { Peer = p.ReadLong(), CharacterUser = p.ReadLong(), CharacterId = p.ReadUInt(), Model = p.ReadString(), Sha256 = p.ReadString() };
                    if (withHeight) s.Height = p.ReadSingle();
                    if (withCalibration)
                    {
                        s.Calibration = p.ReadString();
                        if (!AvatarCalibrationCodec.TryDecode(s.Calibration, out _)) return false;
                    }
                    if (s.Peer == 0 || s.CharacterUser == 0 || s.CharacterId == 0 || !peers.Add(s.Peer) ||
                        !characters.Add(s.CharacterUser + ":" + s.CharacterId) ||
                        !AvatarSyncRules.ValidModel(s.Model) || !AvatarSyncRules.ValidHash(s.Sha256) || !AvatarHeightRules.Valid(s.Height)) return false;
                    result[i] = s;
                }
                if (p.GetPos() != p.Size()) return false;
                states = result; return true;
            }
            catch (Exception) { return false; }
        }
    }
}
