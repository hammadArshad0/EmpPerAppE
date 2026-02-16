using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpPerAppE.Models.DTO.studentevalution
{
	public class EvaluationSubmissionDto
	{
        public int EnrollmentId { get; set; }
        public List<AnswerDto> Answers { get; set; }
    }
    public class AnswerDto
    {
        public int QuestionId { get; set; }
        public int Score { get; set; }
    }
}