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
    }
}
