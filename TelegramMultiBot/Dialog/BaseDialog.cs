﻿// See https://aka.ms/new-console-template for more information
internal abstract class BaseDialog<T> : IDialog where T : struct
{
    private Dictionary<T, T> _states = [];
    public Dictionary<T, T> States { get => _states; }

    public BaseDialog()
    {
        foreach (var state in GetStates())
        {
            States.Add(state.from, state.to);
        }
    }

    public long ChatId { get; set; }
    public bool IsFinished { get; set; }
    public T State { get; set; }

    protected abstract IEnumerable<StateTransition> GetStates();

    public void SetNextState()
    {
        if (States.TryGetValue(State, out var state))
        {
            State = state;
        }
        else
        {
            IsFinished = true;
        }
    }

    protected record StateTransition(T from, T to);
}