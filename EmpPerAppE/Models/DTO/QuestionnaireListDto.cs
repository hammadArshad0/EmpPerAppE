using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpPerAppE.Models.DTO
{
	public class QuestionnaireListDto
	{
        public int Id { get; set; }
        public string Type { get; set; }
        public string Flag { get; set; }
        public int QuestionCount { get; set; }
      
        // Optional: Kitne critical questions hain ye dikhane ke liye
        public int CriticalCount { get; set; }
    }


}