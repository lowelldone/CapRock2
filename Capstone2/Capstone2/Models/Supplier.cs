using System.ComponentModel.DataAnnotations;

namespace Capstone2.Models
{
    public class Supplier
    {
        public int SupplierId { get; set; }

        [Display(Name = "Company Name")]
        public string CompanyName { get; set; }

        [Display(Name = "Contact Person")]
        public string ContactPerson { get; set; }

        [Display(Name = "Contact No.")]
        public string ContactNo { get; set; }
    }
}
