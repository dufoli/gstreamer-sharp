namespace Gst.Base	 {
	using System;
	using System.Runtime.InteropServices;

	partial class Adapter 
	{
		[DllImport("libgstbase-1.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern void gst_adapter_copy(IntPtr raw, out IntPtr dest, int offset, int size);

		public byte[] Copy(int offset, int size) {

			IntPtr mem = Marshal.AllocHGlobal (size);

			gst_adapter_copy(Handle, out mem, offset, size);

			byte[] bytes = new byte[size];
			Marshal.Copy (mem, bytes, 0, size);

			return bytes;
		}

		[DllImport("libgstbase-1.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr gst_adapter_map(IntPtr raw, out int size);

		public byte[] Map() {
			int size;

			IntPtr mem = gst_adapter_map (Handle, out size);
			byte[] ret = new byte[size];
			Marshal.Copy (mem, ret , 0, size);

			return ret;
		}
	}
}