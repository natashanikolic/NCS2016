using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileConversionCommon
{
    public class File
    {
        public int fileId { get; set; }

        [StringLength(2083)]
        [DisplayName("File Attachment")]
        public string fileURL { get; set; }
        [StringLength(2083)]
        [DisplayName("Converted File Link")]
        public string convertedFilelURL { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime postedDate { get; set; }

        [EmailAddress]
        public string destinationEmail { get; set; }
    }
}
