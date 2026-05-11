using System;
using System.Collections.Generic;
using System.Text;

namespace PS2_ISO_Manager.Helper.Manifest;

public class Ps2Manifest
{
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public List<Ps2IsoInfo> Games { get; set; } = new();
}