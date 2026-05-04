using System;
using Domain.Entities;

[Serializable]
public class AnswerDialogRequest
{
    public string playerId;
    public string heroId;
    public string questionId;
    public string answer;
}

[Serializable]
public class AnswerDialogResponse : ApiResponse<AnswerDialogResult>
{
}

[Serializable]
public class AnswerDialogResult
{
    public bool correct;
    public PassiveData assignedPassive;

    public string currentSequence;
    public string nextSequence;

    public bool completed;
    public int pointsAwarded;
}

[Serializable]
public class PassiveData
{
    public int id;
    public string passiveId;
    public string heroId;
    public string name;
    public string assigned_at;
}