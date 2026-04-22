using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpPerAppE.Models.DTO
{
    public class SocietyDTO
    {
        public int SocietyId { get; set; }
        public string SocietyName { get; set; }
        public string Description { get; set; }
    }

    public class SocietyAssignmentDTO
    {
        public string TeacherId { get; set; }
        public int SocietyId { get; set; }
        public int SessionId { get; set; }
        public bool IsChairperson { get; set; }
        public bool IsMentor { get; set; }
    }
}