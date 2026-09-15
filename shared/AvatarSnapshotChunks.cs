using System;
using System.Collections.Generic;

namespace ValheimVRM.Sync
{
    // Keep snapshot parts near 48 KiB even when many players customize every clip.
    // A client replaces its snapshot only after all parts of one revision arrive.
    public static class AvatarSnapshotChunks
    {
        public const int PayloadSize = 48 * 1024;
        public static IEnumerable<ZPackage> Split(long revision, ZPackage snapshot)
        {
            var data = snapshot.GetArray();
            int count = (data.Length + PayloadSize - 1) / PayloadSize;
            for (int index = 0; index < count; index++)
            {
                int length = Math.Min(PayloadSize, data.Length - index * PayloadSize);
                var part = new byte[length]; Buffer.BlockCopy(data, index * PayloadSize, part, 0, length);
                var p = new ZPackage(); p.Write(revision); p.Write(index); p.Write(count); p.Write(data.Length); p.Write(part);
                yield return p;
            }
        }
    }

    public sealed class AvatarSnapshotAssembly
    {
        long revision = -1;
        byte[][] parts;
        int total, received;
        public void Clear() { revision = -1; parts = null; total = received = 0; }
        public bool Add(ZPackage package, long acceptedRevision, out ZPackage snapshot)
        {
            snapshot = null;
            try
            {
                if (package == null || package.Size() > AvatarSnapshotChunks.PayloadSize + 32) return false;
                long next = package.ReadLong();
                int index = package.ReadInt(), count = package.ReadInt(), size = package.ReadInt();
                if (next < 0 || next <= acceptedRevision || next < revision || size <= 0 || size > AvatarSyncWire.MaxSnapshotBytes ||
                    count != (size + AvatarSnapshotChunks.PayloadSize - 1) / AvatarSnapshotChunks.PayloadSize || index < 0 || index >= count) return false;
                int expected = index == count - 1 ? size - index * AvatarSnapshotChunks.PayloadSize : AvatarSnapshotChunks.PayloadSize;
                int start = package.GetPos();
                if (package.ReadInt() != expected || package.Size() - package.GetPos() != expected) return false;
                package.SetPos(start);
                var bytes = package.ReadByteArray();
                if (package.GetPos() != package.Size() || bytes.Length != expected) return false;
                if (parts == null || next > revision)
                { revision = next; parts = new byte[count][]; total = size; received = 0; }
                if (count != parts.Length || total != size || parts[index] != null) return false;
                parts[index] = bytes; received++;
                if (received != count) return false;
                var data = new byte[total]; int position = 0;
                foreach (var part in parts) { Buffer.BlockCopy(part, 0, data, position, part.Length); position += part.Length; }
                var candidate = new ZPackage(data);
                candidate.ReadInt();
                if (candidate.ReadLong() != revision) { Clear(); return false; }
                candidate.SetPos(0); snapshot = candidate; Clear(); return true;
            }
            catch (Exception) { return false; }
        }
    }
}
