namespace CasaEngine.Framework.Audio.Psx;

/// <summary>
/// Reverb unit of the software SPU (task T4.3), written from psx-spx
/// (https://psx-spx.consoledev.net/ps1/spu/soundprocessingunitspu/, sections "SPU Reverb Registers", "SPU Reverb Formula"
/// with its notes "Reverb Disable" and "Reverb Precision", and "SPU Reverb Buffer Resampling"). Driven only by registers:
/// no preset is built in. The 39 FIR coefficients come from <see cref="PsxSpuHardwareTables"/>; without them the unit is
/// bypassed (no output, no work area write, no state change).
///
/// The reverb runs at 22050 Hz: the 44100 Hz input is filtered by the FIR and decimated (one frame in two), the result of
/// the formula is zero-stuffed back to 44100 Hz and filtered by the same FIR. Not thread safe, no allocation after
/// construction.
/// </summary>
internal sealed class PsxSpuReverb
{
    private const int RamSize = PsxSpu.RamSize;
    private const int RegisterCount = 32;
    private const int Taps = PsxSpuHardwareTables.ReverbFirTapCount;

    // Register indices ((address - 1F801DC0h) / 2).
    private const int DApf1 = 0, DApf2 = 1, VIir = 2, VComb1 = 3, VComb2 = 4, VComb3 = 5, VComb4 = 6, VWall = 7, VApf1 = 8, VApf2 = 9;
    private const int MLSame = 10, MRSame = 11, MLComb1 = 12, MRComb1 = 13, MLComb2 = 14, MRComb2 = 15, DLSame = 16, DRSame = 17;
    private const int MLDiff = 18, MRDiff = 19, MLComb3 = 20, MRComb3 = 21, MLComb4 = 22, MRComb4 = 23, DLDiff = 24, DRDiff = 25;
    private const int MLApf1 = 26, MRApf1 = 27, MLApf2 = 28, MRApf2 = 29, VLin = 30, VRin = 31;

    private readonly byte[] _ram;
    private readonly short[] _fir;
    private readonly ushort[] _registers = new ushort[RegisterCount];

    private readonly int[] _inLeft = new int[Taps];
    private readonly int[] _inRight = new int[Taps];
    private readonly int[] _outLeft = new int[Taps];
    private readonly int[] _outRight = new int[Taps];
    private int _head;
    private bool _tickFrame = true; // AMBIGUOUS (psx-spx): which of the two 44100 Hz frames runs the reverb; the first.

    private int _workStart; // byte address of the work area start (ESA * 8)
    private int _bufferAddress; // current buffer address, byte address, even
    private int _volumeLeft;
    private int _volumeRight;

    public PsxSpuReverb(byte[] ram, short[] fir)
    {
        _ram = ram;
        _fir = fir;
    }

    public uint VoiceMask { get; set; }

    public bool Enabled { get; set; }

    public void SetWorkAreaStart(ushort units)
    {
        _workStart = units * 8;
        _bufferAddress = _workStart; // psx-spx: writing ESA also sets the current buffer address.
    }

    public void SetOutputVolume(short left, short right)
    {
        _volumeLeft = left;
        _volumeRight = right;
    }

    public void SetRegister(int index, ushort value)
    {
        if ((uint)index >= RegisterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _registers[index] = value;
    }

    /// <summary>
    /// One 44100 Hz frame: takes the summed reverb-enabled voice output (already saturated to 16 bit) and returns the
    /// reverb output to add to the dry mix.
    /// </summary>
    public void Process(int inputLeft, int inputRight, out int outputLeft, out int outputRight)
    {
        if (_fir == null)
        {
            outputLeft = 0;
            outputRight = 0;
            return;
        }

        _head = _head == 0 ? Taps - 1 : _head - 1; // _head holds the newest sample, older ones at head + i
        _inLeft[_head] = inputLeft;
        _inRight[_head] = inputRight;

        var zeroStuffedLeft = 0;
        var zeroStuffedRight = 0;
        if (_tickFrame)
        {
            Tick(Fir(_inLeft), Fir(_inRight), out zeroStuffedLeft, out zeroStuffedRight);
        }

        _tickFrame = !_tickFrame;
        _outLeft[_head] = zeroStuffedLeft;
        _outRight[_head] = zeroStuffedRight;
        outputLeft = Fir(_outLeft);
        outputRight = Fir(_outRight);
    }

    // y = sum(fir[i] * x[n - i]) / 8000h, saturated. AMBIGUOUS (psx-spx): the page gives the taps but no layout, rounding
    // or gain for the resampling; read literally as a plain FIR on the 44100 Hz stream (decimation on input, zero
    // stuffing on output), arithmetic shift, no gain compensation.
    private int Fir(int[] history)
    {
        var fir = _fir;
        long sum = 0;
        var index = _head;
        for (var i = 0; i < Taps; i++)
        {
            sum += (long)fir[i] * history[index];
            index++;
            if (index == Taps)
            {
                index = 0;
            }
        }

        return Saturate((int)Math.Clamp(sum >> 15, int.MinValue, int.MaxValue));
    }

    // The psx-spx "Reverb Formula", once per 22050 Hz tick. AMBIGUOUS (psx-spx): left and right are both computed in the
    // same tick, as the formula is written (the page says the hardware alternates them on successive 44100 Hz cycles).
    // AMBIGUOUS (psx-spx): every multiplication is divided by 8000h with an arithmetic shift, and every multiplication,
    // addition and subtraction result saturates to 16 bit ("intermediate values DO saturate").
    private void Tick(int firLin, int firRin, out int outLeft, out int outRight)
    {
        var r = _registers;
        var write = Enabled;
        int vIir = (short)r[VIir], vWall = (short)r[VWall], vApf1 = (short)r[VApf1], vApf2 = (short)r[VApf2];

        var lin = Mul(firLin, (short)r[VLin]);
        var rin = Mul(firRin, (short)r[VRin]);

        if (write)
        {
            // Without the master enable the SAME/DIFF stages are not used (psx-spx "Reverb Disable").
            // AMBIGUOUS (psx-spx): vIIR = -8000h negates the stored value on the hardware; not reproduced.
            Same(lin, MLSame, DLSame, vWall, vIir);
            Same(rin, MRSame, DRSame, vWall, vIir);
            Same(lin, MLDiff, DRDiff, vWall, vIir);
            Same(rin, MRDiff, DLDiff, vWall, vIir);
        }

        // The reads still happen when the master enable is clear.
        var lout = Comb(MLComb1, MLComb2, MLComb3, MLComb4);
        var rout = Comb(MRComb1, MRComb2, MRComb3, MRComb4);

        lout = Apf(lout, MLApf1, DApf1, vApf1, write);
        rout = Apf(rout, MRApf1, DApf1, vApf1, write);
        lout = Apf(lout, MLApf2, DApf2, vApf2, write);
        rout = Apf(rout, MRApf2, DApf2, vApf2, write);

        outLeft = Mul(lout, _volumeLeft);
        outRight = Mul(rout, _volumeRight);

        // BufferAddress = MAX(ESA, (BufferAddress + 2) AND 7FFFEh)
        _bufferAddress = Math.Max(_workStart, (_bufferAddress + 2) & (RamSize - 2));
    }

    // [m] = (in + [d]*vWALL - [m-2])*vIIR + [m-2]
    private void Same(int input, int m, int d, int vWall, int vIir)
    {
        var previous = Read(Address(_registers[m] * 8 - 2));
        var wall = Mul(Read(Address(_registers[d] * 8)), vWall);
        var t = Saturate(input + wall);
        t = Saturate(t - previous);
        t = Mul(t, vIir);
        t = Saturate(t + previous);
        Write(Address(_registers[m] * 8), t);
    }

    private int Comb(int c1, int c2, int c3, int c4)
    {
        var r = _registers;
        var sum = Mul(Read(Address(r[c1] * 8)), (short)r[VComb1]);
        sum = Saturate(sum + Mul(Read(Address(r[c2] * 8)), (short)r[VComb2]));
        sum = Saturate(sum + Mul(Read(Address(r[c3] * 8)), (short)r[VComb3]));
        return Saturate(sum + Mul(Read(Address(r[c4] * 8)), (short)r[VComb4]));
    }

    // Lout = Lout - vAPF*[m-dAPF], [m] = Lout, Lout = Lout*vAPF + [m-dAPF]
    private int Apf(int value, int m, int d, int vApf, bool write)
    {
        var delayed = Read(Address(_registers[m] * 8 - _registers[d] * 8));
        value = Saturate(value - Mul(delayed, vApf));
        if (write)
        {
            Write(Address(_registers[m] * 8), value);
        }

        return Saturate(Mul(value, vApf) + delayed);
    }

    // Relative address (bytes) from the current buffer address, wrapped inside the work area ESA..7FFFEh.
    private int Address(int offset)
    {
        var size = RamSize - _workStart;
        var relative = (_bufferAddress + offset - _workStart) % size;
        if (relative < 0)
        {
            relative += size;
        }

        return _workStart + relative;
    }

    private int Read(int address) => (short)(_ram[address] | (_ram[address + 1] << 8));

    private void Write(int address, int value)
    {
        _ram[address] = (byte)value;
        _ram[address + 1] = (byte)(value >> 8);
    }

    private static int Mul(int a, int b) => Saturate((a * b) >> 15);

    private static int Saturate(int v) => v > short.MaxValue ? short.MaxValue : v < short.MinValue ? short.MinValue : v;
}
