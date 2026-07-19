using System.Runtime.InteropServices;

namespace WorldNet;

[StructLayout(LayoutKind.Sequential)]
internal struct FftComplex
{
    public double Real;
    public double Imaginary;
}
