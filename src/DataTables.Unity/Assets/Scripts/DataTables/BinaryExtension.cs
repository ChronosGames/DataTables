using System;
using System.IO;

namespace DataTables
{
    public static class BinaryExtension
    {
        /// <summary>
        /// 从二进制流读取编码后的 32 位有符号整数。
        /// </summary>
        /// <param name="binaryReader">要读取的二进制流。</param>
        /// <returns>读取的 32 位有符号整数。</returns>
        public static int Read7BitEncodedInt32(this BinaryReader binaryReader)
        {
            int value = 0;
            int shift = 0;
            byte b;
            do
            {
                if (shift >= 35)
                {
                    throw new Exception("7 bit encoded int is invalid.");
                }

                b = binaryReader.ReadByte();
                value |= (b & 0x7f) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return value;
        }

        /// <summary>
        /// 向二进制流写入编码后的 32 位有符号整数。
        /// </summary>
        /// <param name="binaryWriter">要写入的二进制流。</param>
        /// <param name="value">要写入的 32 位有符号整数。</param>
        public static void Write7BitEncodedInt32(this BinaryWriter binaryWriter, int value)
        {
            uint num = (uint)value;
            while (num >= 0x80)
            {
                binaryWriter.Write((byte)(num | 0x80));
                num >>= 7;
            }

            binaryWriter.Write((byte)num);
        }

        /// <summary>
        /// 从二进制流读取编码后的 32 位无符号整数。
        /// </summary>
        /// <param name="binaryReader">要读取的二进制流。</param>
        /// <returns>读取的 32 位无符号整数。</returns>
        public static uint Read7BitEncodedUInt32(this BinaryReader binaryReader)
        {
            return (uint)Read7BitEncodedInt32(binaryReader);
        }

        /// <summary>
        /// 向二进制流写入编码后的 32 位无符号整数。
        /// </summary>
        /// <param name="binaryWriter">要写入的二进制流。</param>
        /// <param name="value">要写入的 32 位无符号整数。</param>
        public static void Write7BitEncodedUInt32(this BinaryWriter binaryWriter, uint value)
        {
            Write7BitEncodedInt32(binaryWriter, (int)value);
        }

        /// <summary>
        /// 从二进制流读取编码后的 64 位有符号整数。
        /// </summary>
        /// <param name="binaryReader">要读取的二进制流。</param>
        /// <returns>读取的 64 位有符号整数。</returns>
        public static long Read7BitEncodedInt64(this BinaryReader binaryReader)
        {
            long value = 0L;
            int shift = 0;
            byte b;
            do
            {
                if (shift >= 70)
                {
                    throw new Exception("7 bit encoded int is invalid.");
                }

                b = binaryReader.ReadByte();
                value |= (b & 0x7fL) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return value;
        }

        /// <summary>
        /// 向二进制流写入编码后的 64 位有符号整数。
        /// </summary>
        /// <param name="binaryWriter">要写入的二进制流。</param>
        /// <param name="value">要写入的 64 位有符号整数。</param>
        public static void Write7BitEncodedInt64(this BinaryWriter binaryWriter, long value)
        {
            ulong num = (ulong)value;
            while (num >= 0x80)
            {
                binaryWriter.Write((byte)(num | 0x80));
                num >>= 7;
            }

            binaryWriter.Write((byte)num);
        }

        /// <summary>
        /// 从二进制流读取编码后的 64 位无符号整数。
        /// </summary>
        /// <param name="binaryReader">要读取的二进制流。</param>
        /// <returns>读取的 64 位无符号整数。</returns>
        public static ulong Read7BitEncodedUInt64(this BinaryReader binaryReader)
        {
            return (ulong)Read7BitEncodedInt64(binaryReader);
        }

        /// <summary>
        /// 向二进制流写入编码后的 64 位无符号整数。
        /// </summary>
        /// <param name="binaryWriter">要写入的二进制流。</param>
        /// <param name="value">要写入的 64 位无符号整数。</param>
        public static void Write7BitEncodedUInt64(this BinaryWriter binaryWriter, ulong value)
        {
            Write7BitEncodedInt64(binaryWriter, (long)value);
        }

        public static T? ReadJson<T>(this BinaryReader binaryReader)
        {
            string plain = binaryReader.ReadString();
            if (string.IsNullOrEmpty(plain))
            {
                return default;
            }

#if NET7_0_OR_GREATER
            return System.Text.Json.JsonSerializer.Deserialize<T>(plain);
#elif UNITY_EDITOR
            return UnityEngine.JsonUtility.FromJson<T>(plain);
#else
            throw new NotImplementedException();
#endif
        }
    }
}
