using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpPerAppE.Models.DTO
{
	public class EmailRequestDTO
	{
        public string mail { get; set; }
        public string filter { get; set; } // "unread", "read", "all"
    }
}