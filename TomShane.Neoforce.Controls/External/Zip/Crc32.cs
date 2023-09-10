using System;

namespace TomShane.Neoforce.External.Zip;

internal class Crc32
{
    private UInt32[] _crc32Table;
    private const int BufferSize = 8192;

    public Int32 TotalBytesRead { get; private set; }

    public UInt32 GetCrc32(System.IO.Stream input)
    {
        return GetCrc32AndCopy(input, null);
    }

    public UInt32 GetCrc32AndCopy(System.IO.Stream input, System.IO.Stream output)
    {
        unchecked
        {
            UInt32 crc32Result;
            crc32Result = 0xFFFFFFFF;
            var buffer = new byte[BufferSize];
            var readSize = BufferSize;

            TotalBytesRead = 0;
            var count = input.Read(buffer, 0, readSize);
            output?.Write(buffer, 0, count);

            TotalBytesRead += count;
            while (count > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    crc32Result = (crc32Result >> 8) ^ _crc32Table[buffer[i] ^ (crc32Result & 0x000000FF)];
                }
                count = input.Read(buffer, 0, readSize);
                output?.Write(buffer, 0, count);

                TotalBytesRead += count;

            }

            return ~crc32Result;
        }
    }


    public Crc32()
    {
        unchecked
        {
            // This is the official polynomial used by CRC32 in PKZip.
            // Often the polynomial is shown reversed as 0x04C11DB7.
            var dwPolynomial = 0xEDB88320;
            UInt32 i, j;

            _crc32Table = new UInt32[256];

            UInt32 dwCrc;
            for (i = 0; i < 256; i++)
            {
                dwCrc = i;
                for (j = 8; j > 0; j--)
                {
                    if ((dwCrc & 1) == 1)
                    {
                        dwCrc = (dwCrc >> 1) ^ dwPolynomial;
                    }
                    else
                    {
                        dwCrc >>= 1;
                    }
                }
                _crc32Table[i] = dwCrc;
            }
        }
    }
}