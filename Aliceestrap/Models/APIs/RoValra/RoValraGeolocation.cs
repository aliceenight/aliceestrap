using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aliceestrap.Models.APIs.RoValra
{
    public class RoValraGeolocation
    {
        [JsonPropertyName("location")]
        public RoValraServerLocation? Location { get; set; } = null!;
    }
}
