﻿using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing
{
    // Outgoing message queue
    public partial class RpcProcessor
    {
        /// <summary>
        /// Create an outgoing message queue
        /// </summary>
        /// <returns>Outgoing message queue</returns>
        protected virtual OutgoingQueue CreateOutgoingMessageQueue() => new(this)
        {
            Name = "Outgoing RPC message queue"
        };

        /// <summary>
        /// Outgoing message queue
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        /// <param name="comparer">Message comparer</param>
        protected class OutgoingQueue(in RpcProcessor processor, in IComparer<OutgoingQueue.QueuedMessage>? comparer = null)
            : PriorityItemQueueWorkerBase<OutgoingQueue.QueuedMessage, OutgoingQueue.QueuedMessage>(
                processor.Options.OutgoingMessageQueueCapacity,
                comparer ?? new MessageComparer()
                )
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <summary>
            /// Logger
            /// </summary>
            public ILogger? Logger => Processor.Logger;

            /// <inheritdoc/>
            protected override async Task ProcessItem(QueuedMessage item, CancellationToken cancellationToken)
            {
                try
                {
                    Logger?.Log(LogLevel.Trace, "{this} processing outgoing message #{id} ({type})", ToString(), item.Message.Id, item.Message.GetType());
                    if (item.CancelToken.IsCancellationRequested || item.IsDone)
                    {
                        Logger?.Log(LogLevel.Trace, "{this} processing outgoing message #{id} ({type}) was done already or is canceled", ToString(), item.Message.Id, item.Message.GetType());
                        item.Completion.TrySetException(new OperationCanceledException("Canceled before processing"));
                        return;
                    }
                    using (Cancellations cancellation = new(cancellationToken, item.CancelToken))
                        await Processor.SendMessageAsync(item.Message, cancellation).DynamicContext();
                    Logger?.Log(LogLevel.Trace, "{this} outgoing message #{id} ({type}) processed", ToString(), item.Message.Id, item.Message.GetType());
                    item.Completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    Logger?.Log(LogLevel.Trace, "{this} outgoing message #{id} ({type}) processing failed", ToString(), item.Message.Id, item.Message.GetType());
                    item.Completion.TrySetException(ex);
                }
                finally
                {
                    item.SetDone();
                    Logger?.Log(LogLevel.Trace, "{this} processing outgoing message #{id} ({type}) done", ToString(), item.Message.Id, item.Message.GetType());
                }
            }

            /// <summary>
            /// Queued outgoing message
            /// </summary>
            /// <remarks>
            /// Constructor
            /// </remarks>
            public record class QueuedMessage()
            {
                /// <summary>
                /// Enqueued time
                /// </summary>
                public DateTime Enqueued { get; } = DateTime.Now;

                /// <summary>
                /// Done time
                /// </summary>
                public DateTime Done { get; protected set; } = DateTime.MinValue;

                /// <summary>
                /// If the message has been processed
                /// </summary>
                public bool IsDone => Completion.Task.IsCompleted || Done != DateTime.MinValue;

                /// <summary>
                /// Message
                /// </summary>
                public required IRpcMessage Message { get; init; }

                /// <summary>
                /// Priority (higher value will be processed faster)
                /// </summary>
                public required int Priority { get; init; }

                /// <summary>
                /// Cancellation
                /// </summary>
                public required CancellationToken CancelToken { get; init; }

                /// <summary>
                /// Completion
                /// </summary>
                public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

                /// <summary>
                /// Set <see cref="Done"/>
                /// </summary>
                public virtual void SetDone()
                {
                    if (Done == DateTime.MinValue)
                        Done = DateTime.Now;
                }
            }

            /// <summary>
            /// Message comparer
            /// </summary>
            /// <remarks>
            /// Constructor
            /// </remarks>
            private sealed class MessageComparer() : IComparer<QueuedMessage>
            {
                /// <inheritdoc/>
                public int Compare(QueuedMessage? x, QueuedMessage? y)
                {
                    if (x is null || y is null) throw new InvalidProgramException();
                    if (x.Priority < y.Priority) return -1;
                    if (x.Priority > y.Priority) return 1;
                    if (x.Enqueued < y.Enqueued) return -1;
                    if (x.Enqueued > y.Enqueued) return 1;
                    if(x.Message.Id.HasValue && y.Message.Id.HasValue)
                    {
                        if (x.Message.Id.Value > y.Message.Id.Value) return -1;
                        if (x.Message.Id.Value < y.Message.Id.Value) return 1;
                    }
                    return 0;
                }
            }
        }
    }
}
