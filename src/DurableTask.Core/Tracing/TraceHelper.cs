﻿//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------
#nullable enable
namespace DurableTask.Core.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.ExceptionServices;
    using DurableTask.Core.Common;
    using DurableTask.Core.History;

    /// <summary>
    ///     Helper class for logging/tracing
    /// </summary>
    public class TraceHelper
    {
        const string Source = "DurableTask";

        // TODO: Add tracing for external event send, which could be used to initialize an entity
        static readonly ActivitySource ActivityTraceSource = new ActivitySource(Source);

        internal static Activity? CreateActivityForNewOrchestration(ExecutionStartedEvent startEvent)
        {
            Activity? newActivity = ActivityTraceSource.StartActivity(
                name: startEvent.Name,
                kind: ActivityKind.Internal,
                parentContext: Activity.Current?.Context ?? default,
                tags: new KeyValuePair<string, object?>[]
                {
                    new("dt.type", "client"),
                    new("dt.instanceid", startEvent.OrchestrationInstance.InstanceId),
                    new("dt.executionid", startEvent.OrchestrationInstance.ExecutionId),
                });

            if (newActivity != null)
            {
                startEvent.SetParentTraceContext(newActivity);
            }

            return newActivity;
        }

        /// <summary>
        /// Starts a new trace activity for orchestration execution.
        /// </summary>
        /// <param name="startEvent">The orchestration's execution started event.</param>
        /// <returns>
        /// Returns a newly started <see cref="Activity"/> with orchestration-specific metadata.
        /// </returns>
        internal static Activity? StartTraceActivityForExecution(ExecutionStartedEvent? startEvent)
        {
            if (startEvent == null)
            {
                return null;
            }

            if (!startEvent.TryGetParentTraceContext(out ActivityContext activityContext))
            {
                return null;
            }

            return ActivityTraceSource.StartActivity(
                name: startEvent.Name,
                kind: ActivityKind.Internal,
                parentContext: activityContext,
                tags: new KeyValuePair<string, object?>[]
                {
                    new("dt.type", "orchestrator"),
                    new("dt.instanceid", startEvent.OrchestrationInstance.InstanceId),
                    new("dt.executionid", startEvent.OrchestrationInstance.ExecutionId),
                });
        }

        /// <summary>
        /// Starts a new trace activity for (task) activity execution.
        /// </summary>
        /// <param name="scheduledEvent">The associated <see cref="TaskScheduledEvent"/>.</param>
        /// <param name="instance">The associated orchestration instance metadata.</param>
        /// <returns>
        /// Returns a newly started <see cref="Activity"/> with (task) activity and orchestration-specific metadata.
        /// </returns>
        internal static Activity? StartTraceActivityForTask(
            TaskScheduledEvent scheduledEvent,
            OrchestrationInstance instance)
        {
            if (!scheduledEvent.TryGetParentTraceContext(out ActivityContext activityContext))
            {
                return null;
            }

            return ActivityTraceSource.StartActivity(
                name: $"{scheduledEvent.Name} (#{scheduledEvent.EventId})",
                kind: ActivityKind.Internal,
                parentContext: activityContext,
                tags: new KeyValuePair<string, object?>[]
                {
                    new("dt.type", "activity"),
                    new("dt.instanceid", instance.InstanceId),
                    new("dt.executionid", instance.ExecutionId),
                    new("dt.taskid", scheduledEvent.EventId),
                });
        }

        /// <summary>
        ///     Simple trace with no iid or eid
        /// </summary>
        public static void Trace(TraceEventType eventLevel, string eventType, Func<string> generateMessage)
        {
            ExceptionHandlingWrapper(
                () => DefaultEventSource.Log.TraceEvent(eventLevel, Source, string.Empty, string.Empty, string.Empty, generateMessage(), eventType));
        }

        /// <summary>
        ///     Simple trace with no iid or eid
        /// </summary>
        public static void Trace(TraceEventType eventLevel, string eventType, string format, params object[] args)
        {
            ExceptionHandlingWrapper(
                () => DefaultEventSource.Log.TraceEvent(eventLevel, Source, string.Empty, string.Empty, string.Empty, FormatString(format, args), eventType));
        }

        /// <summary>
        ///     Trace with iid but no eid
        /// </summary>
        public static void TraceSession(TraceEventType eventLevel, string eventType, string sessionId, Func<string> generateMessage)
        {
            ExceptionHandlingWrapper(
                () => DefaultEventSource.Log.TraceEvent(eventLevel, Source, string.Empty, string.Empty, sessionId, generateMessage(), eventType));
        }

        /// <summary>
        ///     Trace with iid but no eid
        /// </summary>
        public static void TraceSession(TraceEventType eventLevel, string eventType, string sessionId, string format, params object[] args)
        {
            ExceptionHandlingWrapper(
                () => DefaultEventSource.Log.TraceEvent(eventLevel, Source, string.Empty, string.Empty, sessionId, FormatString(format, args), eventType));
        }

        /// <summary>
        ///     Trace with iid and eid
        /// </summary>
        public static void TraceInstance(TraceEventType eventLevel, string eventType, OrchestrationInstance orchestrationInstance,
            string format, params object[] args)
        {
            ExceptionHandlingWrapper(
                () => DefaultEventSource.Log.TraceEvent(
                    eventLevel,
                    Source,
                    orchestrationInstance == null ? string.Empty : orchestrationInstance.InstanceId,
                    orchestrationInstance == null ? string.Empty : orchestrationInstance.ExecutionId,
                    string.Empty,
                    FormatString(format, args),
                    eventType));
        }

        /// <summary>
        ///     Trace with iid and eid
        /// </summary>
        public static void TraceInstance(TraceEventType eventLevel, string eventType, OrchestrationInstance orchestrationInstance,
            Func<string> generateMessage)
        {
            ExceptionHandlingWrapper(
                () => DefaultEventSource.Log.TraceEvent(
                    eventLevel,
                    Source,
                    orchestrationInstance == null ? string.Empty : orchestrationInstance.InstanceId,
                    orchestrationInstance == null ? string.Empty : orchestrationInstance.ExecutionId,
                    string.Empty,
                    generateMessage(),
                    eventType));
        }

        /// <summary>
        ///     Trace an exception
        /// </summary>
        public static Exception TraceException(TraceEventType eventLevel, string eventType, Exception exception)
        {
            return TraceException(eventLevel, eventType, exception, string.Empty);
        }

        /// <summary>
        ///     Trace an exception and message
        /// </summary>
        public static Exception TraceException(TraceEventType eventLevel, string eventType, Exception exception,
            Func<string> generateMessage)
        {
            return TraceExceptionCore(eventLevel, eventType, string.Empty, string.Empty, exception, generateMessage);
        }

        /// <summary>
        ///     Trace an exception and message
        /// </summary>
        public static Exception TraceException(TraceEventType eventLevel, string eventType, Exception exception, string format,
            params object[] args)
        {
            return TraceExceptionCore(eventLevel, eventType, string.Empty, string.Empty, ExceptionDispatchInfo.Capture(exception), format, args).SourceException;
        }

        /// <summary>
        ///     Trace an instance exception
        /// </summary>
        public static Exception TraceExceptionInstance(TraceEventType eventLevel, string eventType,
            OrchestrationInstance orchestrationInstance, Exception exception)
        {
            return TraceExceptionCore(eventLevel, eventType, orchestrationInstance.InstanceId, orchestrationInstance.ExecutionId,
                ExceptionDispatchInfo.Capture(exception), string.Empty).SourceException;
        }

        /// <summary>
        ///     Trace an instance exception and message
        /// </summary>
        public static Exception TraceExceptionInstance(TraceEventType eventLevel, string eventType,
            OrchestrationInstance orchestrationInstance, Exception exception, Func<string> generateMessage)
        {
            return TraceExceptionCore(eventLevel, eventType, orchestrationInstance.InstanceId, orchestrationInstance.ExecutionId,
                exception, generateMessage);
        }

        /// <summary>
        ///     Trace an instance exception and message
        /// </summary>
        public static Exception TraceExceptionInstance(TraceEventType eventLevel, string eventType,
            OrchestrationInstance orchestrationInstance, Exception exception, string format, params object[] args)
        {
            return TraceExceptionCore(eventLevel, eventType, orchestrationInstance.InstanceId, orchestrationInstance.ExecutionId,
                ExceptionDispatchInfo.Capture(exception), format, args).SourceException;
        }

        /// <summary>
        ///     Trace a session exception without execution id
        /// </summary>
        public static Exception TraceExceptionSession(TraceEventType eventLevel, string eventType, string sessionId, Exception exception)
        {
            return TraceExceptionCore(eventLevel, eventType, sessionId, string.Empty, ExceptionDispatchInfo.Capture(exception), string.Empty).SourceException;
        }

        /// <summary>
        ///     Trace a session exception without execution id
        /// </summary>
        public static ExceptionDispatchInfo TraceExceptionSession(TraceEventType eventLevel, string eventType, string sessionId, ExceptionDispatchInfo exceptionDispatchInfo)
        {
            return TraceExceptionCore(eventLevel, eventType, sessionId, string.Empty, exceptionDispatchInfo, string.Empty);
        }

        /// <summary>
        ///     Trace a session exception and message without execution id
        /// </summary>
        public static Exception TraceExceptionSession(TraceEventType eventLevel, string eventType, string sessionId, Exception exception,
            Func<string> generateMessage)
        {
            return TraceExceptionCore(eventLevel, eventType, sessionId, string.Empty, exception, generateMessage);
        }

        /// <summary>
        ///     Trace a session exception and message without execution id
        /// </summary>
        public static Exception TraceExceptionSession(TraceEventType eventLevel, string eventType, string sessionId, Exception exception,
            string format, params object[] args)
        {
            return TraceExceptionCore(eventLevel, eventType, sessionId, string.Empty, ExceptionDispatchInfo.Capture(exception), format, args).SourceException;
        }

        /// <summary>
        ///     Trace a session exception and message without execution id
        /// </summary>
        public static ExceptionDispatchInfo TraceExceptionSession(TraceEventType eventLevel, string eventType, string sessionId, ExceptionDispatchInfo exceptionDispatchInfo,
            string format, params object[] args)
        {
            return TraceExceptionCore(eventLevel, eventType, sessionId, string.Empty, exceptionDispatchInfo, format, args);
        }

        // helper methods
        static ExceptionDispatchInfo TraceExceptionCore(TraceEventType eventLevel, string eventType, string iid, string eid, ExceptionDispatchInfo exceptionDispatchInfo,
            string format, params object[] args)
        {
            Exception exception = exceptionDispatchInfo.SourceException;

            string newFormat = format + "\nException: " + exception.GetType() + " : " + exception.Message + "\n\t" +
                               exception.StackTrace + "\nInner Exception: " +
                               exception.InnerException?.ToString();

            ExceptionHandlingWrapper(
                () => DefaultEventSource.Log.TraceEvent(eventLevel, Source, iid, eid, string.Empty, FormatString(newFormat, args), eventType));

            return exceptionDispatchInfo;
        }

        static Exception TraceExceptionCore(TraceEventType eventLevel, string eventType, string iid, string eid, Exception exception,
            Func<string> generateMessage)
        {
            string newFormat = generateMessage() + "\nException: " + exception.GetType() + " : " + exception.Message +
                               "\n\t" + exception.StackTrace + "\nInner Exception: " +
                               exception.InnerException?.ToString();

            ExceptionHandlingWrapper(
                () => DefaultEventSource.Log.TraceEvent(eventLevel, Source, iid, eid, string.Empty, newFormat, eventType));

            return exception;
        }

        static string FormatString(string formatted, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return formatted;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, formatted, args);
            }
            catch (FormatException ex)
            {
                string message = string.Format(CultureInfo.InvariantCulture, "String FormatException for '{0}'. Args count: {1}. Exception: {2}", formatted, args.Length, ex);
                DefaultEventSource.Log.TraceEvent(TraceEventType.Error, Source, string.Empty, string.Empty, string.Empty, message, "LogFormattingFailed");

                return formatted;
            }
        }

        static void ExceptionHandlingWrapper(Action innerFunc)
        {
            try
            {
                innerFunc();
            }
            catch (Exception exception) when (!Utils.IsFatal(exception))
            {
                try
                {
                    DefaultEventSource.Log.TraceEvent(TraceEventType.Error, Source, string.Empty, string.Empty, string.Empty, exception, "WriteEventFailed");
                }
                catch (Exception anotherException) when (!Utils.IsFatal(anotherException))
                {
                }
            }
        }
    }
}