// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Represents a class to run an executable and capture the output and error streams.
    /// </summary>
    public class CommandRunner
    {
        private readonly string _arguments;
        private readonly IDictionary<string, string> _environmentVariables;
        private readonly StringBuilder _error = new();
        private readonly string _filename;
        private readonly Action<StreamWriter> _inputAction;
        private readonly StringBuilder _output = new();
        private readonly int _timeoutInMilliseconds;
        private readonly string _workingDirectory;

        private CommandRunner(string filename, string arguments, string workingDirectory, int timeoutInMilliseconds = 60000, Action<StreamWriter> inputAction = null, IDictionary<string, string> environmentVariables = null)
        {
            _filename = filename;
            _arguments = arguments;
            _workingDirectory = workingDirectory;
            _timeoutInMilliseconds = timeoutInMilliseconds;
            _inputAction = inputAction;
            _environmentVariables = environmentVariables;
        }

        /// <summary>
        /// Runs the specified executable and returns the result.
        /// </summary>
        /// <param name="filename">The path to the executable to run.</param>
        /// <param name="workingDirectory">An optional working directory to use when running the executable.</param>
        /// <param name="arguments">Optional command-line arguments to pass to the executable.</param>
        /// <param name="timeOutInMilliseconds">Optional amount of milliseconds to wait for the executable to exit before returning.</param>
        /// <param name="inputAction">An optional <see cref="Action{T}" /> to invoke against the executables input stream.</param>
        /// <param name="environmentVariables">An optional <see cref="Dictionary{TKey, TValue}" /> containing environment variables to specify when running the executable.</param>
        /// <returns>A <see cref="CommandRunnerResult" /> containing details about the result of the running the executable including the exit code and console output.</returns>
        public static CommandRunnerResult Run(string filename, string workingDirectory = null, string arguments = null, int timeOutInMilliseconds = 60000, Action<StreamWriter> inputAction = null, IDictionary<string, string> environmentVariables = null)
        {
            CommandRunner commandRunner = new(filename, arguments ?? string.Empty, workingDirectory ?? Environment.CurrentDirectory, timeOutInMilliseconds, inputAction, environmentVariables);

            return commandRunner.Run();
        }

        public CommandRunnerResult Run()
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo(Path.GetFullPath(_filename), _arguments)
                {
                    WorkingDirectory = Path.GetFullPath(_workingDirectory),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = _inputAction != null
                }
            };

            process.StartInfo.Environment["NuGetTestModeEnabled"] = bool.TrueString;

            if (_environmentVariables != null)
            {
                foreach (var pair in _environmentVariables)
                {
                    process.StartInfo.EnvironmentVariables[pair.Key] = pair.Value;
                }
            }

            process.OutputDataReceived += OnOutputDataReceived;
            process.ErrorDataReceived += OnErrorDataReceived;

            bool started = process.Start();

            if (!started)
            {
                throw new Exception($"Failed to start process {process.StartInfo.FileName} {process.StartInfo.Arguments}");
            }

            _inputAction?.Invoke(process.StandardInput);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            bool exited = process.WaitForExit(_timeoutInMilliseconds);

            if (exited)
            {
                Task task = Task.Run(() =>
                {
                    // This overload ensures that all processing has been completed, including the handling of asynchronous events for redirected standard output.
                    // You should use this overload after a call to the WaitForExit(Int32) overload when standard output has been redirected to asynchronous event handlers.
                    process.WaitForExit();
                });

                exited = task.Wait(millisecondsTimeout: 5000);

                return new CommandRunnerResult(process.ExitCode, _output.ToString(), _error.ToString());
            }

            string extraInfo = string.Empty;

            if (!TryKill(process, out Exception processException))
            {
                extraInfo = $"Failed to kill the process: {processException?.ToString() ?? "Timed out waiting for the process to be terminated"}";
            }

            throw new TimeoutException($"{process.StartInfo.FileName} {process.StartInfo.Arguments} timed out after {TimeSpan.FromMilliseconds(_timeoutInMilliseconds).TotalSeconds:N0} seconds:{Environment.NewLine}Output:{_output}{Environment.NewLine}Error:{_error}{Environment.NewLine}{extraInfo}");
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs args)
        {
            if (args.Data != null)
            {
                _error.AppendLine(args.Data);
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs args)
        {
            if (args.Data != null)
            {
                _output.AppendLine(args.Data);
            }
        }

        private bool TryKill(Process process, out Exception exception)
        {
            exception = null;

            try
            {
                Task task = Task.Run(() => process.Kill());

                bool exited = task.Wait(5000);

                if (!exited)
                {
                    exception = new TimeoutException("Timed out waiting for process to end after attempting to kill it.", innerException: exception);
                }

                return exited;
            }
            catch (Exception e)
            {
                exception = e;
            }

            return false;
        }
    }
}
