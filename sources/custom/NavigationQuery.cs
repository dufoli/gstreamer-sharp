namespace Gst.Video {

	using System;
	using System.Runtime.InteropServices;

	public partial class NavigationAdapter {

		public static bool ParseCommands (Gst.Query query, out NavigationCommand[] cmds) {
			uint len;

			cmds = null;
			if (!QueryParseCommandsLength (query, out len))
				return false;

			cmds = new NavigationCommand[len];

			for (uint i = 0; i < len; i++) {
				NavigationCommand cmd;

				if (!QueryParseCommandsNth (query, i, out cmd))
					return false;
				cmds[i] = cmd;
			}

			return true;
		}
	}
}