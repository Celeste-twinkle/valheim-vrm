using System;
using System.Collections.Generic;
using System.Linq;

namespace ValheimVRM.Sync
{
    public sealed class AvatarSelection
    {
        public long Peer;
        public long CharacterUser;
        public uint CharacterId;
        public string Model;
        public string Sha256;
        public bool SameAs(AvatarSelection other) => other != null && Peer == other.Peer &&
            CharacterUser == other.CharacterUser && CharacterId == other.CharacterId &&
            Model == other.Model && Sha256 == other.Sha256;
    }

    public static class AvatarSyncRules
    {
        public const int Version = 1;
        public const int MaxPlayers = 128;
        public static bool ValidModel(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 255 &&
            name != "." && name != ".." && !name.EndsWith(".", StringComparison.Ordinal) &&
            !name.EndsWith(" ", StringComparison.Ordinal) &&
            !name.Any(c => char.IsControl(c) || "\\/:*?\"<>|".IndexOf(c) >= 0);
        public static bool ValidHash(string hash) => hash != null && hash.Length == 64 &&
            hash.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    }

    // One instance per authenticated connection, not per character or nickname.
    // Zero denotes legacy packets; after the first sequenced packet, late legacy
    // packets cannot downgrade the connection and overwrite its newer state.
    public sealed class AvatarRequestOrder
    {
        public long LastSequence { get; private set; }
        public bool TryAccept(long sequence)
        {
            if (sequence == 0) return LastSequence == 0;
            if (sequence < 0 || sequence <= LastSequence) return false;
            LastSequence = sequence; return true;
        }
    }

    // Keys are authenticated connection IDs; character IDs come from the server's
    // player ZDO. Neither character names nor a global selected model identify a player.
    public sealed class AvatarSyncRegistry
    {
        readonly Dictionary<long, AvatarSelection> selections = new Dictionary<long, AvatarSelection>();
        public long Revision { get; private set; }
        public AvatarSelection[] Snapshot() => selections.Values.OrderBy(s => s.Peer).ToArray();
        public bool Remove(long peer)
        {
            if (!selections.Remove(peer)) return false;
            Revision++; return true;
        }
        public bool Set(long authenticatedPeer, long characterUser, uint characterId, string model, string hash)
        {
            if (authenticatedPeer == 0 || characterUser == 0 || characterId == 0 ||
                !AvatarSyncRules.ValidModel(model) || !AvatarSyncRules.ValidHash(hash)) return false;
            if (!selections.ContainsKey(authenticatedPeer) && selections.Count >= AvatarSyncRules.MaxPlayers) return false;
            if (selections.Values.Any(s => s.Peer != authenticatedPeer && s.CharacterUser == characterUser && s.CharacterId == characterId)) return false;
            var next = new AvatarSelection { Peer = authenticatedPeer, CharacterUser = characterUser,
                CharacterId = characterId, Model = model, Sha256 = hash };
            if (selections.TryGetValue(authenticatedPeer, out var current) && current.SameAs(next)) return false;
            selections[authenticatedPeer] = next; Revision++; return true;
        }
    }
}
