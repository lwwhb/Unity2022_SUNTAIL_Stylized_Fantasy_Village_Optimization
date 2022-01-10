using UnityEngine;
using System.IO;
#if UNITY_2018_2_OR_NEWER
using Unity.Collections;
#endif
namespace UPRProfiler
{
    public class ByteArray
    {
        private MemoryStream stream;
        private BinaryWriter writer;


        public ByteArray()
        {
            stream = new MemoryStream();
            writer = new BinaryWriter(stream);
        }

        public void writeByte(byte value)
        {
            writer.Write(value);
        }

        public byte[] GetAllBytes()
        {
            byte[] buffer = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(buffer, 0, (int)stream.Length);
            reset();
            return buffer;
        }
        public void reset()
        {
            this.stream.Dispose();
        }
    }

    class BitString
    {
        public int len = 0;
        public int val = 0;
    }

    public class BitmapData
    {
        public int height;
        public int width;

        private byte[] rawbyte;
        Color result = new Color();

        public BitmapData(byte[] rawbyte, int _width, int _height)
        {
            this.rawbyte = rawbyte;
            height = _height;
            width = _width;
        }

        public Color getPixelColor(int x, int y, int type)
        {
            if (x >= width)
                x = width - 1;

            if (y >= height)
                y = height - 1;

            if (x < 0)
                x = 0;

            if (y < 0)
                y = 0;

            result.r = (float)rawbyte[y * width * 3 + x * 3];
            result.g = (float)rawbyte[y * width * 3 + x * 3 + 1];
            result.b = (float)rawbyte[y * width * 3 + x * 3 + 2];
            return result;
        }

#if UNITY_2018_2_OR_NEWER
        private NativeArray<byte> nativeRawByte;

        public BitmapData(NativeArray<byte> nativerawbyte, int _width, int _height)
        {
            this.nativeRawByte = nativerawbyte;
            height = _height;
            width = _width;
        }

        public Color getNativePixelColor(int x, int y, int type)
        {
            if (x >= width)
                x = width - 1;

            if (y >= height)
                y = height - 1;

            if (x < 0)
                x = 0;

            if (y < 0)
                y = 0;

            result.r = (float)nativeRawByte[y * width * 3 + x * 3];
            result.g = (float)nativeRawByte[y * width * 3 + x * 3 + 1];
            result.b = (float)nativeRawByte[y * width * 3 + x * 3 + 2];
            return result;
        }
#endif
    }

    public class JPGEncoder
    {

        public int[] ZigZag = new int[64] {
         0, 1, 5, 6,14,15,27,28,
         2, 4, 7,13,16,26,29,42,
         3, 8,12,17,25,30,41,43,
         9,11,18,24,31,40,44,53,
        10,19,23,32,39,45,52,54,
        20,22,33,38,46,51,55,60,
        21,34,37,47,50,56,59,61,
        35,36,48,49,57,58,62,63
    };
        int[] YQT = new int[64] {
            16, 11, 10, 16, 24, 40, 51, 61,
            12, 12, 14, 19, 26, 58, 60, 55,
            14, 13, 16, 24, 40, 57, 69, 56,
            14, 17, 22, 29, 51, 87, 80, 62,
            18, 22, 37, 56, 68,109,103, 77,
            24, 35, 55, 64, 81,104,113, 92,
            49, 64, 78, 87,103,121,120,101,
            72, 92, 95, 98,112,100,103, 99
        };
        int[] UVQT = new int[64] {
            17, 18, 24, 47, 99, 99, 99, 99,
            18, 21, 26, 66, 99, 99, 99, 99,
            24, 26, 56, 99, 99, 99, 99, 99,
            47, 66, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99
        };
        float[] aasf = new float[8] {
            1.0f, 1.387039845f, 1.306562965f, 1.175875602f,
            1.0f, 0.785694958f, 0.541196100f, 0.275899379f
        };
        private int[] YTable = new int[64];
        private int[] UVTable = new int[64];
        private float[] fdtbl_Y = new float[64];
        private float[] fdtbl_UV = new float[64];

        private void initQuantTables(int sf)
        {
            int i;
            float t;

            for (i = 0; i < 64; i++)
            {
                t = Mathf.Floor((YQT[i] * sf + 50.0f) / 100.0f);
                if (t < 1.0f)
                {
                    t = 1.0f;
                }
                else if (t > 255.0f)
                {
                    t = 255.0f;
                }
                YTable[ZigZag[i]] = (int)t;
            }



            for (i = 0; i < 64; i++)
            {
                t = Mathf.Floor((UVQT[i] * sf + 50.0f) / 100.0f);
                if (t < 1.0f)
                {
                    t = 1.0f;
                }
                else if (t > 255.0f)
                {
                    t = 255.0f;
                }
                UVTable[ZigZag[i]] = (int)t;
            }



            i = 0;
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    fdtbl_Y[i] = (1.0f / (YTable[ZigZag[i]] * aasf[row] * aasf[col] * 8.0f));
                    fdtbl_UV[i] = (1.0f / (UVTable[ZigZag[i]] * aasf[row] * aasf[col] * 8.0f));
                    i++;
                }
            }
        }

        private BitString[] YDC_HT;
        private BitString[] UVDC_HT;
        private BitString[] YAC_HT;
        private BitString[] UVAC_HT;
        private BitString[] HT;
        private int[] std_dc_luminance_nrcodes = new int[17] { 0, 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
        private int[] std_dc_luminance_values = new int[12] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        private int[] std_ac_luminance_nrcodes = new int[17] { 0, 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d };
        private int[] std_ac_luminance_values = new int[162] {
        0x01,0x02,0x03,0x00,0x04,0x11,0x05,0x12,
        0x21,0x31,0x41,0x06,0x13,0x51,0x61,0x07,
        0x22,0x71,0x14,0x32,0x81,0x91,0xa1,0x08,
        0x23,0x42,0xb1,0xc1,0x15,0x52,0xd1,0xf0,
        0x24,0x33,0x62,0x72,0x82,0x09,0x0a,0x16,
        0x17,0x18,0x19,0x1a,0x25,0x26,0x27,0x28,
        0x29,0x2a,0x34,0x35,0x36,0x37,0x38,0x39,
        0x3a,0x43,0x44,0x45,0x46,0x47,0x48,0x49,
        0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,
        0x5a,0x63,0x64,0x65,0x66,0x67,0x68,0x69,
        0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,
        0x7a,0x83,0x84,0x85,0x86,0x87,0x88,0x89,
        0x8a,0x92,0x93,0x94,0x95,0x96,0x97,0x98,
        0x99,0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,
        0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,0xb5,0xb6,
        0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,
        0xc6,0xc7,0xc8,0xc9,0xca,0xd2,0xd3,0xd4,
        0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe1,0xe2,
        0xe3,0xe4,0xe5,0xe6,0xe7,0xe8,0xe9,0xea,
        0xf1,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,
        0xf9,0xfa
    };
        private int[] std_dc_chrominance_nrcodes = new int[17] { 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
        private int[] std_dc_chrominance_values = new int[12] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        private int[] std_ac_chrominance_nrcodes = new int[17] { 0, 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 };
        private int[] std_ac_chrominance_values = new int[162] {
        0x00,0x01,0x02,0x03,0x11,0x04,0x05,0x21,
        0x31,0x06,0x12,0x41,0x51,0x07,0x61,0x71,
        0x13,0x22,0x32,0x81,0x08,0x14,0x42,0x91,
        0xa1,0xb1,0xc1,0x09,0x23,0x33,0x52,0xf0,
        0x15,0x62,0x72,0xd1,0x0a,0x16,0x24,0x34,
        0xe1,0x25,0xf1,0x17,0x18,0x19,0x1a,0x26,
        0x27,0x28,0x29,0x2a,0x35,0x36,0x37,0x38,
        0x39,0x3a,0x43,0x44,0x45,0x46,0x47,0x48,
        0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,
        0x59,0x5a,0x63,0x64,0x65,0x66,0x67,0x68,
        0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,
        0x79,0x7a,0x82,0x83,0x84,0x85,0x86,0x87,
        0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,
        0x97,0x98,0x99,0x9a,0xa2,0xa3,0xa4,0xa5,
        0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,
        0xb5,0xb6,0xb7,0xb8,0xb9,0xba,0xc2,0xc3,
        0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,0xd2,
        0xd3,0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,
        0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,0xe8,0xe9,
        0xea,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,
        0xf9,0xfa
    };
        private BitString[] bitcode = new BitString[65535];
        private int[] category = new int[65535];

        private BitString[] computeHuffmanTbl(int[] nrcodes, int[] std_table)
        {
            int codevalue = 0;
            int pos_in_table = 0;
            HT = new BitString[16 * 16];
            for (int k = 1; k <= 16; k++)
            {
                for (int j = 1; j <= nrcodes[k]; j++)
                {
                    HT[std_table[pos_in_table]] = new BitString();
                    HT[std_table[pos_in_table]].val = codevalue;
                    HT[std_table[pos_in_table]].len = k;
                    pos_in_table++;
                    codevalue++;
                }
                codevalue *= 2;
            }
            return HT;
        }

        private void initHuffmanTbl()
        {
            YDC_HT = computeHuffmanTbl(std_dc_luminance_nrcodes, std_dc_luminance_values);
            UVDC_HT = computeHuffmanTbl(std_dc_chrominance_nrcodes, std_dc_chrominance_values);
            YAC_HT = computeHuffmanTbl(std_ac_luminance_nrcodes, std_ac_luminance_values);
            UVAC_HT = computeHuffmanTbl(std_ac_chrominance_nrcodes, std_ac_chrominance_values);
        }

        private void initCategoryfloat()
        {
            int nrlower = 1;
            int nrupper = 2;
            int nr;
            BitString bs;

            for (int cat = 1; cat <= 15; cat++)
            {
                //Positive numbers
                for (nr = nrlower; nr < nrupper; nr++)
                {
                    category[32767 + nr] = cat;

                    bs = new BitString();
                    bs.len = cat;
                    bs.val = nr;
                    bitcode[32767 + nr] = bs;
                }
                //Negative numbers
                for (nr = -(nrupper - 1); nr <= -nrlower; nr++)
                {
                    category[32767 + nr] = cat;

                    bs = new BitString();
                    bs.len = cat;
                    bs.val = nrupper - 1 + nr;
                    bitcode[32767 + nr] = bs;
                }
                nrlower <<= 1;
                nrupper <<= 1;
            }
        }

        private int bytenew = 0;
        private int bytepos = 7;
        public ByteArray byteout = new ByteArray();

        public byte[] GetBytes()
        {
            if (!isDone)
            {
                Debug.LogError("JPEGEncoder not complete, cannot get bytes!");
                return new byte[1];
            }

            return byteout.GetAllBytes();
        }

        private void writeBits(BitString bs)
        {
            int value = bs.val;
            int posval = bs.len - 1;
            while (posval >= 0)
            {
                if (((uint)value & System.Convert.ToUInt32(1 << posval)) != 0)
                {
                    bytenew |= (int)(System.Convert.ToUInt32(1 << bytepos));
                }
                posval--;
                bytepos--;
                if (bytepos < 0)
                {
                    if (bytenew == 0xFF)
                    {
                        writeByte(0xFF);
                        writeByte(0);
                    }
                    else
                    {
                        writeByte((byte)bytenew);
                    }
                    bytepos = 7;
                    bytenew = 0;
                }
            }
        }

        private void writeByte(byte value)
        {
            byteout.writeByte(value);
        }

        private void writeWord(int value)
        {
            writeByte((byte)((value >> 8) & 0xFF));
            writeByte((byte)((value) & 0xFF));
        }

        private float[] fDCTQuant(float[] data, float[] fdtbl)
        {
            float tmp0; float tmp1; float tmp2; float tmp3; float tmp4; float tmp5; float tmp6; float tmp7;
            float tmp10; float tmp11; float tmp12; float tmp13;

            float z1; float z2; float z3; float z4; float z5; float z11; float z13;

            int i;

            /* Pass 1: process rows. */
            int dataOff = 0;
            for (i = 0; i < 8; i++)
            {
                tmp0 = data[dataOff + 0] + data[dataOff + 7];
                tmp7 = data[dataOff + 0] - data[dataOff + 7];
                tmp1 = data[dataOff + 1] + data[dataOff + 6];
                tmp6 = data[dataOff + 1] - data[dataOff + 6];
                tmp2 = data[dataOff + 2] + data[dataOff + 5];
                tmp5 = data[dataOff + 2] - data[dataOff + 5];
                tmp3 = data[dataOff + 3] + data[dataOff + 4];
                tmp4 = data[dataOff + 3] - data[dataOff + 4];


                tmp10 = tmp0 + tmp3;
                tmp13 = tmp0 - tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp1 - tmp2;

                data[dataOff + 0] = tmp10 + tmp11;
                data[dataOff + 4] = tmp10 - tmp11;

                z1 = (tmp12 + tmp13) * 0.707106781f;
                data[dataOff + 2] = tmp13 + z1;
                data[dataOff + 6] = tmp13 - z1;


                tmp10 = tmp4 + tmp5;
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;


                z5 = (tmp10 - tmp12) * 0.382683433f;
                z2 = 0.541196100f * tmp10 + z5;
                z4 = 1.306562965f * tmp12 + z5;
                z3 = tmp11 * 0.707106781f;

                z11 = tmp7 + z3;
                z13 = tmp7 - z3;

                data[dataOff + 5] = z13 + z2;
                data[dataOff + 3] = z13 - z2;
                data[dataOff + 1] = z11 + z4;
                data[dataOff + 7] = z11 - z4;

                dataOff += 8;
            }

            dataOff = 0;
            for (i = 0; i < 8; i++)
            {
                tmp0 = data[dataOff + 0] + data[dataOff + 56];
                tmp7 = data[dataOff + 0] - data[dataOff + 56];
                tmp1 = data[dataOff + 8] + data[dataOff + 48];
                tmp6 = data[dataOff + 8] - data[dataOff + 48];
                tmp2 = data[dataOff + 16] + data[dataOff + 40];
                tmp5 = data[dataOff + 16] - data[dataOff + 40];
                tmp3 = data[dataOff + 24] + data[dataOff + 32];
                tmp4 = data[dataOff + 24] - data[dataOff + 32];


                tmp10 = tmp0 + tmp3;
                tmp13 = tmp0 - tmp3;
                tmp11 = tmp1 + tmp2;
                tmp12 = tmp1 - tmp2;

                data[dataOff + 0] = tmp10 + tmp11;
                data[dataOff + 32] = tmp10 - tmp11;

                z1 = (tmp12 + tmp13) * 0.707106781f;
                data[dataOff + 16] = tmp13 + z1;
                data[dataOff + 48] = tmp13 - z1;


                tmp10 = tmp4 + tmp5;
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;


                z5 = (tmp10 - tmp12) * 0.382683433f;
                z2 = 0.541196100f * tmp10 + z5;
                z4 = 1.306562965f * tmp12 + z5;
                z3 = tmp11 * 0.707106781f;

                z11 = tmp7 + z3;
                z13 = tmp7 - z3;

                data[dataOff + 40] = z13 + z2;
                data[dataOff + 24] = z13 - z2;
                data[dataOff + 8] = z11 + z4;
                data[dataOff + 56] = z11 - z4;

                dataOff++;
            }


            for (i = 0; i < 64; i++)
            {
                data[i] = Mathf.Round((data[i] * fdtbl[i]));
            }
            return data;
        }


        private void writeAPP0()
        {
            writeWord(0xFFE0);
            writeWord(16);
            writeByte(0x4A);
            writeByte(0x46);
            writeByte(0x49);
            writeByte(0x46);
            writeByte(0);
            writeByte(1);
            writeByte(1);
            writeByte(0);
            writeWord(1);
            writeWord(1);
            writeByte(0);
            writeByte(0);
        }

        private void writeSOF0(int width, int height)
        {
            writeWord(0xFFC0);
            writeWord(17);
            writeByte(8);
            writeWord(height);
            writeWord(width);
            writeByte(3);
            writeByte(1);
            writeByte(0x11);
            writeByte(0);
            writeByte(2);
            writeByte(0x11);
            writeByte(1);
            writeByte(3);
            writeByte(0x11);
            writeByte(1);
        }

        private void writeDQT()
        {
            writeWord(0xFFDB);
            writeWord(132);
            writeByte(0);
            int i;
            for (i = 0; i < 64; i++)
            {
                writeByte((byte)(YTable[i]));
            }
            writeByte(1);
            for (i = 0; i < 64; i++)
            {
                writeByte((byte)(UVTable[i]));
            }
        }

        private void writeDHT()
        {
            writeWord(0xFFC4);
            writeWord(0x01A2);
            int i;

            writeByte(0);
            for (i = 0; i < 16; i++)
            {
                writeByte((byte)(std_dc_luminance_nrcodes[i + 1]));
            }
            for (i = 0; i <= 11; i++)
            {
                writeByte((byte)(std_dc_luminance_values[i]));
            }

            writeByte(0x10);
            for (i = 0; i < 16; i++)
            {
                writeByte((byte)(std_ac_luminance_nrcodes[i + 1]));
            }
            for (i = 0; i <= 161; i++)
            {
                writeByte((byte)(std_ac_luminance_values[i]));
            }

            writeByte(1);
            for (i = 0; i < 16; i++)
            {
                writeByte((byte)(std_dc_chrominance_nrcodes[i + 1]));
            }
            for (i = 0; i <= 11; i++)
            {
                writeByte((byte)(std_dc_chrominance_values[i]));
            }

            writeByte(0x11);
            for (i = 0; i < 16; i++)
            {
                writeByte((byte)(std_ac_chrominance_nrcodes[i + 1]));
            }
            for (i = 0; i <= 161; i++)
            {
                writeByte((byte)(std_ac_chrominance_values[i]));
            }
        }

        private void writeSOS()
        {
            writeWord(0xFFDA);
            writeWord(12);
            writeByte(3);
            writeByte(1);
            writeByte(0);
            writeByte(2);
            writeByte(0x11);
            writeByte(3);
            writeByte(0x11);
            writeByte(0);
            writeByte(0x3f);
            writeByte(0);
        }
        private int[] DU = new int[64];
        private float[] YDU = new float[64];
        private float[] UDU = new float[64];
        private float[] VDU = new float[64];

        private float processDU(float[] CDU, float[] fdtbl, float DC, BitString[] HTDC, BitString[] HTAC)
        {
            BitString EOB = HTAC[0x00];
            BitString M16zeroes = HTAC[0xF0];
            int i;

            float[] DU_DCT = fDCTQuant(CDU, fdtbl);


            for (i = 0; i < 64; i++)
            {
                DU[ZigZag[i]] = (int)(DU_DCT[i]);
            }
            int Diff = (int)(DU[0] - DC);
            DC = DU[0];

            if (Diff == 0)
            {
                writeBits(HTDC[0]);
            }
            else
            {
                writeBits(HTDC[category[32767 + Diff]]);
                writeBits(bitcode[32767 + Diff]);
            }

            int end0pos = 63;
            for (; (end0pos > 0) && (DU[end0pos] == 0); end0pos--)
            {
            };
            if (end0pos == 0)
            {
                writeBits(EOB);
                return DC;
            }
            i = 1;
            while (i <= end0pos)
            {
                int startpos = i;
                for (; (DU[i] == 0) && (i <= end0pos); i++)
                {
                }
                int nrzeroes = i - startpos;
                if (nrzeroes >= 16)
                {
                    for (int nrmarker = 1; nrmarker <= nrzeroes / 16; nrmarker++)
                    {
                        writeBits(M16zeroes);
                    }
                    nrzeroes = (nrzeroes & 0xF);
                }
                writeBits(HTAC[nrzeroes * 16 + category[32767 + DU[i]]]);
                writeBits(bitcode[32767 + DU[i]]);
                i++;
            }
            if (end0pos != 63)
            {
                writeBits(EOB);
            }
            return DC;
        }

        private void RGB2YUV(BitmapData img, int xpos, int ypos)
        {
            int pos = 0;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    Color C;
#if UNITY_2018_2_OR_NEWER

                    C = img.getNativePixelColor(xpos + x, img.height - (ypos + y), 1);
#else

                    C = img.getPixelColor(xpos + x, img.height - (ypos + y), 1);
#endif
                    float R = C.r;
                    float G = C.g;
                    float B = C.b;
                    YDU[pos] = (((0.29900f) * R + (0.58700f) * G + (0.11400f) * B)) - 128.0f;
                    UDU[pos] = (((-0.16874f) * R + (-0.33126f) * G + (0.50000f) * B));
                    VDU[pos] = (((0.50000f) * R + (-0.41869f) * G + (-0.08131f) * B));
                    pos++;
                }
            }
        }

        public bool isDone = false;
        private BitmapData image;
        private int sf = 0;

        public JPGEncoder(float quality)
        {

            if (quality <= 0.0f)
                quality = 1.0f;

            if (quality > 100.0f)
                quality = 100.0f;

            if (quality < 50.0f)
                sf = (int)(5000.0f / quality);
            else
                sf = (int)(200.0f - quality * 2.0f);

            initHuffmanTbl();
            initCategoryfloat();
            initQuantTables(sf);
        }


        public void doEncoding(byte[] rawByte, int width, int height)
        {

            image = new BitmapData(rawByte, width, height);
            isDone = false;
            encode();

            isDone = true;

            image = null;
        }
#if UNITY_2018_2_OR_NEWER
        public void doNativeEncoding(NativeArray<byte> rawByte, int width, int height)
        {

            image = new BitmapData(rawByte, width, height);
            isDone = false;
            encode();
            isDone = true;
            image = null;
        }
#endif

        private void encode()
        {
            // Initialize bit writer
            byteout = new ByteArray();
            bytenew = 0;
            bytepos = 7;

            // Add JPEG headers
            writeWord(0xFFD8); // SOI
            writeAPP0();
            writeDQT();
            writeSOF0(image.width, image.height);
            writeDHT();
            writeSOS();

            // Encode 8x8 macroblocks
            float DCY = 0.0f;
            float DCU = 0.0f;
            float DCV = 0.0f;
            bytenew = 0;
            bytepos = 7;
            for (int ypos = 0; ypos < image.height; ypos += 8)
            {
                for (int xpos = 0; xpos < image.width; xpos += 8)
                {
                    RGB2YUV(image, xpos, ypos);
                    DCY = processDU(YDU, fdtbl_Y, DCY, YDC_HT, YAC_HT);
                    DCU = processDU(UDU, fdtbl_UV, DCU, UVDC_HT, UVAC_HT);
                    DCV = processDU(VDU, fdtbl_UV, DCV, UVDC_HT, UVAC_HT);
                }
            }

            if (bytepos >= 0)
            {
                BitString fillbits = new BitString();
                fillbits.len = bytepos + 1;
                fillbits.val = (1 << (bytepos + 1)) - 1;
                writeBits(fillbits);
            }

            writeWord(0xFFD9);
            isDone = true;
        }
    }
}
