using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPort.Application.DTOs.Response
{
    
        public class AssetCheckResponseDto
        {
            public bool Success { get; set; }
            public List<AssetCheckResult> Results { get; set; } = new();
        }

}
