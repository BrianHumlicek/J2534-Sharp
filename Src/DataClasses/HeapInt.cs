#region License
/*Copyright(c) 2018, Brian Humlicek
* https://github.com/BrianHumlicek
* 
*Permission is hereby granted, free of charge, to any person obtaining a copy
*of this software and associated documentation files (the "Software"), to deal
*in the Software without restriction, including without limitation the rights
*to use, copy, modify, merge, publish, distribute, sub-license, and/or sell
*copies of the Software, and to permit persons to whom the Software is
*furnished to do so, subject to the following conditions:
*The above copyright notice and this permission notice shall be included in all
*copies or substantial portions of the Software.
*
*THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
*IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
*FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
*AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
*LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
*OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
*SOFTWARE.
*/
#endregion License
using System;
using System.Runtime.InteropServices;

namespace SAE.J2534
{
  internal class HeapInt : IDisposable
  {
    public IntPtr Ptr { get; }
    public HeapInt()
    {
      Ptr = Marshal.AllocHGlobal(4);
    }
    public HeapInt(int i)
    {
      Ptr = Marshal.AllocHGlobal(4);
      Marshal.WriteInt32(Ptr, i);
    }
    public int Value
        {
            get => Marshal.ReadInt32(Ptr);
            set => Marshal.WriteInt32(Ptr, value);
        }
    public static explicit operator IntPtr(HeapInt HeapIntPtr)
    {
      return HeapIntPtr.Ptr;
    }

    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          // TODO: dispose managed state (managed objects).
        }

        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
        // TODO: set large fields to null.
        Marshal.FreeHGlobal(Ptr);

        disposedValue = true;
      }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    ~HeapInt() {
      // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
      Dispose(false);
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
      // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
      Dispose(true);
      // TODO: uncomment the following line if the finalizer is overridden above.
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
