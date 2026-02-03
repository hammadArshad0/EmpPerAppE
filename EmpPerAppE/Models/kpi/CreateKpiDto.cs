using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpPerAppE.Models.kpi
{
	public class CreateKpiDto
    {
		
    public int SessionId { get; set; }
        public int EmployeeTypeId { get; set; }
        public string KpiName { get; set; }
        public int KpiWeight { get; set; }
        public List<SubKpiDto> SubKpis { get; set; }
    }

    public class SubKpiDto
    {
        public int? SubKpiId { get; set; }   // for updates
        public string Name { get; set; }
        public int Weight { get; set; }      // must sum to 100
    }

    public class UpdateKpiWeightDto
    {
        public int SessionId { get; set; }
        public int EmployeeTypeId { get; set; }
        public int NewWeight { get; set; }
    }
}
