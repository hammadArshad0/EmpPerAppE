using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpPerAppE.Models.DTO
{
    public class ProjectDTOs
    {


        public class CommitteeStatusDTO
        {
            public string UserID { get; set; }
            public bool IsMember { get; set; }
        }

        public class ProjectEvaluationRequest
        {
            public string EvaluatorId { get; set; } // Director ID
            public string EvaluateeId { get; set; } // Teacher ID
            public int SessionId { get; set; }
            public List<QuestionScoreDTO> Evaluations { get; set; }
        }

        public class QuestionScoreDTO
        {
            public int QuestionId { get; set; }
            public int Score { get; set; } // 4, 3, 2, 1 logic
        }
    }
}