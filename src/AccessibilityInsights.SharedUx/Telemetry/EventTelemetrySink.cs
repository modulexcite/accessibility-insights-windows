﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using AccessibilityInsights.Extensions;
using System;

namespace AccessibilityInsights.SharedUx.Telemetry
{
    /// <summary>
    /// Listens for telemetry events raised by extensions
    /// and passes the events down to the TelemetrySink.
    /// </summary>
    internal static class EventTelemetrySink
    {
        private static bool IsReportExceptionHandlerAttached = false;
        private readonly static object LockObject = new object();
        private readonly static ReportExceptionBuffer ReportExceptionBuffer = new ReportExceptionBuffer(TelemetrySink.ReportException);

        static EventTelemetrySink()
        {
            // Automatically start listening for telemetry events raised by extensions
            EventTelemetrySink.AttachReportExceptionHandler();
        }

        public static void Enable()
        {
            // Report any exceptions which may have been caught before telemetry was enabled.
            ReportExceptionBuffer.EnableForwarding();

            // we report this problem after the buffer is flushed
            // because the above call also opens future exceptions to be passed through
            // and in case telemetry is allowed later, we want all the exceptions we can get
            if (!TelemetrySink.IsEnabled)
                TelemetrySink.ReportException(new Exception("Telemetry was lost! Exceptions were flushed from ReportExceptionBuffer, but the telemetry sink was not open."));
        }

        /// <summary>
        /// Forward exceptions from the event handler to the logger
        /// </summary>
        private static void OnReportedException(object sender, ReportExceptionEventArgs args)
        {
            ReportExceptionBuffer.ReportException(args.ReportedException);
        }

        /// <summary>
        /// Attach the handler only once, no matter how many times this method gets called
        /// </summary>
        private static void AttachReportExceptionHandler()
        {
            if (IsReportExceptionHandlerAttached) return;

            lock (LockObject)
            {
                if (IsReportExceptionHandlerAttached) return;

                Container.ReportedExceptionEvent += OnReportedException;
                IsReportExceptionHandlerAttached = true;
            }
        }
    } // class
} // namespace
