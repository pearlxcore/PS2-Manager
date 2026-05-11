using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace PS2_ISO_Manager.Helper;

public static class DgvSmoothScroll
{
    public static void EnableDoubleBuffer(DataGridView dgv)
    {
        typeof(DataGridView)
            .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(dgv, true, null);

        dgv.GetType()
           .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
           ?.SetValue(dgv, true, null);

        dgv.ScrollBars = ScrollBars.Both; // optional but helps
    }
}