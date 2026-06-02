using PastPort.Application.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PastPort.Application.DTOs.Response
{
  
        public class AssetSyncResponseDto
        {
            public bool Success { get; set; }
            public int TotalAssets { get; set; }
            public List<AssetDto> Assets { get; set; } = new();
            public string? Message { get; set; }
        }
   
}



