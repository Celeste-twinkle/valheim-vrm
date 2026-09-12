using System;
using System.Collections.Generic;

namespace ValheimVRM.Sync
{
    public static class AvatarSyncWire
    {
        public const string Hello = "ValheimVRM.Sync.Hello";
        public const string SequencedHello = "ValheimVRM.Sync.SequencedHello";
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
        public static bool ReadSelection(ZPackage p, out bool enabled, out string model, out string hash)
        {
            return ReadSelection(p, out enabled, out model, out hash, out var sequence) && sequence == 0;
        }
        public static bool ReadSelection(ZPackage p, out bool enabled, out string model, out string hash, out long sequence)
        {
            enabled = false; model = hash = ""; sequence = 0;
            try
            {
                if (p == null || p.Size() > 2048 || p.ReadInt() != AvatarSyncRules.Version) return false;
                enabled = p.ReadBool(); model = p.ReadString(); hash = p.ReadString();
                if (p.GetPos() != p.Size())
                {
                    if (p.Size() - p.GetPos() != sizeof(long)) return false;
                    sequence = p.ReadLong();
                    if (sequence <= 0) return false;
                }
                return p.GetPos() == p.Size() && (model == "" && hash == "" ||
                    AvatarSyncRules.ValidModel(model) && AvatarSyncRules.ValidHash(hash));
            }
            catch (Exception) { return false; }
        }
        public static ZPackage Snapshot(long revision, AvatarSelection[] states)
        {
            var p = new ZPackage(); p.Write(AvatarSyncRules.Version); p.Write(revision); p.Write(states.Length);
            foreach (var s in states)
            {
                p.Write(s.Peer); p.Write(s.CharacterUser); p.Write(s.CharacterId); p.Write(s.Model); p.Write(s.Sha256);
            }
            return p;
        }
        public static bool ReadSnapshot(ZPackage p, out long revision, out AvatarSelection[] states)
        {
            revision = -1; states = null;
            try
            {
                if (p == null || p.Size() > 256 * 1024 || p.ReadInt() != AvatarSyncRules.Version) return false;
                revision = p.ReadLong(); int count = p.ReadInt();
                if (revision < 0 || count < 0 || count > AvatarSyncRules.MaxPlayers) return false;
                var peers = new HashSet<long>(); var characters = new HashSet<string>();
                var result = new AvatarSelection[count];
                for (int i = 0; i < count; i++)
                {
                    var s = new AvatarSelection { Peer = p.ReadLong(), CharacterUser = p.ReadLong(), CharacterId = p.ReadUInt(), Model = p.ReadString(), Sha256 = p.ReadString() };
                    if (s.Peer == 0 || s.CharacterUser == 0 || s.CharacterId == 0 || !peers.Add(s.Peer) ||
                        !characters.Add(s.CharacterUser + ":" + s.CharacterId) ||
                        !AvatarSyncRules.ValidModel(s.Model) || !AvatarSyncRules.ValidHash(s.Sha256)) return false;
                    result[i] = s;
                }
                if (p.GetPos() != p.Size()) return false;
                states = result; return true;
            }
            catch (Exception) { return false; }
        }
    }
}
