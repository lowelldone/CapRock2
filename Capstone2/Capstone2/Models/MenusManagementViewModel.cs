using System.Collections.Generic;
using Capstone2.Models;

namespace Capstone2.Models
{
    public class MenusManagementViewModel
    {
        public IEnumerable<Menu> Menus { get; set; } = new List<Menu>();
        public IEnumerable<MenuPackages> MenuPackagesList { get; set; } = new List<MenuPackages>();
    }
}
