using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    namespace PastPort.Application.Common;

    public class VrGeneratorSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int RequestTimeoutMinutes { get; set; } = 8;
    }

