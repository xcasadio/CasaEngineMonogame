using System;
using System.Collections.Generic;
using CasaEngine.Editor.Controls;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// MGUI message boxes plan, T2.2 (D8, ADR-0041): the editor shows one question at a time, in the order asked, and the
/// next one only once the previous one is answered — including when an answer asks a new question, which is how the
/// asynchronous close, quit and open-world flows chain their questions.
/// </summary>
public class EditorMessageBoxQueueTests
{
    private static readonly string[] YesNo = ["Yes", "No"];

    /// <summary>A presenter that records what it shows and lets the test answer it.</summary>
    private sealed class FakePresenter
    {
        public List<string> Shown { get; } = new();
        public List<Action<int>> Answers { get; } = new();

        public void Present(EditorMessageBoxQueue.Request request, Action<int> answered)
        {
            Shown.Add(request.Title);
            Answers.Add(answered);
        }

        public void AnswerLast(int index) => Answers[^1](index);
    }

    private static EditorMessageBoxQueue.Request Question(string title, Action<int> answered = null)
        => new(title, "message", YesNo, 0, 1, answered);

    [Fact]
    public void TheFirstQuestionIsShownAtOnce_TheOthersWait_ThenComeInOrder()
    {
        FakePresenter presenter = new();
        EditorMessageBoxQueue queue = new(presenter.Present);

        queue.Enqueue(Question("A"));
        queue.Enqueue(Question("B"));
        queue.Enqueue(Question("C"));
        Assert.Equal(new[] { "A" }, presenter.Shown);

        presenter.AnswerLast(0);
        Assert.Equal(new[] { "A", "B" }, presenter.Shown);

        presenter.AnswerLast(1);
        Assert.Equal(new[] { "A", "B", "C" }, presenter.Shown);

        presenter.AnswerLast(0);
        Assert.False(queue.HasPendingOrOpen);
    }

    [Fact]
    public void EachAnswerReachesItsOwnQuestion_Once()
    {
        FakePresenter presenter = new();
        EditorMessageBoxQueue queue = new(presenter.Present);
        List<string> answers = new();

        queue.Enqueue(Question("A", index => answers.Add($"A:{index}")));
        queue.Enqueue(Question("B", index => answers.Add($"B:{index}")));
        Action<int> answerA = presenter.Answers[0];
        answerA(1);
        answerA(0); // a stale second answer from the presenter is ignored
        presenter.AnswerLast(0);

        Assert.Equal(new[] { "A:1", "B:0" }, answers);
    }

    [Fact]
    public void HasPendingOrOpen_FollowsTheQueue()
    {
        FakePresenter presenter = new();
        EditorMessageBoxQueue queue = new(presenter.Present);
        Assert.False(queue.HasPendingOrOpen);

        queue.Enqueue(Question("A"));
        Assert.True(queue.HasPendingOrOpen);

        presenter.AnswerLast(0);
        Assert.False(queue.HasPendingOrOpen);
    }

    [Fact]
    public void AQuestionAskedFromAnAnswer_ComesAfterTheQuestionsAlreadyWaiting()
    {
        FakePresenter presenter = new();
        EditorMessageBoxQueue queue = new(presenter.Present);

        queue.Enqueue(Question("A", _ => queue.Enqueue(Question("C"))));
        queue.Enqueue(Question("B"));

        presenter.AnswerLast(0); // answering A asks C, while B is already waiting
        Assert.Equal(new[] { "A", "B" }, presenter.Shown);

        presenter.AnswerLast(0);
        Assert.Equal(new[] { "A", "B", "C" }, presenter.Shown);
    }

    [Fact]
    public void AQuestionAskedFromTheLastAnswer_IsShownAtOnce()
    {
        FakePresenter presenter = new();
        EditorMessageBoxQueue queue = new(presenter.Present);

        queue.Enqueue(Question("A", _ => queue.Enqueue(Question("B"))));
        presenter.AnswerLast(0);

        Assert.Equal(new[] { "A", "B" }, presenter.Shown);
        Assert.True(queue.HasPendingOrOpen);
    }

    [Fact]
    public void AnAnswerThatThrows_DoesNotStallTheQueue()
    {
        FakePresenter presenter = new();
        EditorMessageBoxQueue queue = new(presenter.Present);

        queue.Enqueue(Question("A", _ => throw new InvalidOperationException("answer failed")));
        queue.Enqueue(Question("B"));

        Assert.Throws<InvalidOperationException>(() => presenter.AnswerLast(0));
        Assert.Equal(new[] { "A", "B" }, presenter.Shown);
    }

    [Fact]
    public void APresenterThatThrows_DoesNotStallTheQueue()
    {
        bool fail = true;
        List<string> shown = new();
        EditorMessageBoxQueue queue = new((request, _) =>
        {
            if (fail)
            {
                throw new InvalidOperationException("presenter failed");
            }

            shown.Add(request.Title);
        });

        Assert.Throws<InvalidOperationException>(() => queue.Enqueue(Question("A")));
        Assert.False(queue.HasPendingOrOpen);

        fail = false;
        queue.Enqueue(Question("B"));
        Assert.Equal(new[] { "B" }, shown);
    }

    [Fact]
    public void AMessageWithoutAnswerHandler_StillHandsOverToTheNextQuestion()
    {
        FakePresenter presenter = new();
        EditorMessageBoxQueue queue = new(presenter.Present);

        queue.Enqueue(new EditorMessageBoxQueue.Request("Error", "failed", ["OK"], 0, 0, null));
        queue.Enqueue(Question("B"));
        presenter.AnswerLast(0);

        Assert.Equal(new[] { "Error", "B" }, presenter.Shown);
    }

    [Fact]
    public void InvalidQuestions_FailEarly_WithoutBlockingTheQueue()
    {
        FakePresenter presenter = new();
        EditorMessageBoxQueue queue = new(presenter.Present);

        Assert.Throws<ArgumentException>(() => queue.Enqueue(new EditorMessageBoxQueue.Request("t", "m", null, 0, 0, null)));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(new EditorMessageBoxQueue.Request("t", "m", [], 0, 0, null)));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(new EditorMessageBoxQueue.Request("t", "m", ["1", "2", "3", "4"], 0, 0, null)));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(new EditorMessageBoxQueue.Request("t", "m", YesNo, 2, 0, null)));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(new EditorMessageBoxQueue.Request("t", "m", YesNo, 0, -1, null)));
        Assert.Throws<ArgumentNullException>(() => new EditorMessageBoxQueue(null));

        Assert.False(queue.HasPendingOrOpen);
        queue.Enqueue(Question("A"));
        Assert.Equal(new[] { "A" }, presenter.Shown);
    }
}
