using System.Collections.Generic;
using System.Linq;
using Domain.Entities;

public class DialogRunner
{
    private Dictionary<string, DialogNode> nodeMap;
    private DialogMetadata metadata;

    public string CurrentSequence { get; private set; }

    public void Initialize(DialogMetadata metadata)
    {
        this.metadata = metadata;

        nodeMap = metadata.nodes.ToDictionary(n => n.sequence, n => n);

        CurrentSequence = metadata.startSequence;
    }

    public DialogNode GetCurrentNode()
    {
        if (nodeMap.TryGetValue(CurrentSequence, out var node))
            return node;

        return null;
    }

    public void GoNext()
    {
        var current = GetCurrentNode();

        if (current == null || string.IsNullOrEmpty(current.nextSequence))
            return;

        CurrentSequence = current.nextSequence;
    }

    public void Answer(string optionKey)
    {
        var current = GetCurrentNode();

        if (current == null || !current.isQuestion)
            return;

        var answer = current.possibleAnswers
            .FirstOrDefault(a => a.optionKey == optionKey);

        if (answer != null)
        {
            CurrentSequence = answer.nextSequence;
        }
    }

    public bool IsCompleted()
    {
        return CurrentSequence == metadata.completedSequence;
    }

    public void SetSequence(string sequence)
    {
        CurrentSequence = sequence;
    }
}