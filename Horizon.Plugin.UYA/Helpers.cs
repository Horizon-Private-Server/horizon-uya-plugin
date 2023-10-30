using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server.Common;
using Server.Common.Stream;

namespace Horizon.Plugin.UYA
{
    public static class Helpers
    {

        #region MessageReader

        public static T[] ReadArray<T>(this MessageReader reader, int count)
        {
            T[] results = new T[count];
            for (int i = 0; i < count; ++i)
                results[i] = reader.Read<T>();
            return results;
        }

        public static void Align(this MessageReader reader, int alignment)
        {
            long mod = reader.BaseStream.Position % alignment;
            if (mod == 0) return;

            // move forward to reach alignment
            reader.ReadBytes(alignment - (int)mod);
        }

        public static void Align(this MessageWriter writer, int alignment)
        {
            long mod = writer.BaseStream.Position % alignment;
            if (mod == 0) return;

            // move forward to reach alignment
            writer.Write(new byte[alignment - (int)mod]);
        }

        #endregion

    }
}
