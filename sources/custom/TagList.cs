namespace Gst
{
	using System;
	using System.Runtime.InteropServices;

	public partial class TagList
	{
		public object this [string tag, uint index] {
		 	get { return GetValueIndex (tag, index).Val; }
		}

		public object this [string tag] {
			get {
				GLib.Value v;
				bool success;

				success = CopyValue (out v, this, tag);

				if (!success)
					return null;

				object ret = (object)v.Val;
				v.Dispose ();

				return ret;
			}
		}

		public void Add (Gst.TagMergeMode mode, string tag, object value)
		{
			if (!Tag.Exists (tag))
				throw new ArgumentException (String.Format ("Invalid tag name '{0}'", tag));

			GLib.Value v = new GLib.Value (value);

			AddValue (mode, tag, v);
			v.Dispose ();
		}
	}
}