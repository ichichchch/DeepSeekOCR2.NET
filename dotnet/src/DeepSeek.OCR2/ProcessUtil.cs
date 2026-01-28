using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeek.OCR2;

internal static class ProcessUtil
{
    public static void AddArguments(ProcessStartInfo psi, IReadOnlyList<string> arguments)
    {
        if (psi is null) throw new ArgumentNullException(nameof(psi));
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));

#if NET6_0_OR_GREATER
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);
#else
        psi.Arguments = BuildArguments(arguments);
#endif
    }

    public static async Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));

#if NET6_0_OR_GREATER
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
#else
        while (!process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
#endif
    }

    private static string BuildArguments(IReadOnlyList<string> arguments)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < arguments.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(QuoteArgument(arguments[i]));
        }
        return sb.ToString();
    }

    private static string QuoteArgument(string arg)
    {
        if (arg is null) return "\"\"";
        if (arg.Length == 0) return "\"\"";

        var needsQuotes = false;
        for (var i = 0; i < arg.Length; i++)
        {
            var c = arg[i];
            if (char.IsWhiteSpace(c) || c == '"' || c == '\\')
            {
                needsQuotes = true;
                break;
            }
        }

        if (!needsQuotes) return arg;

        var sb = new StringBuilder(arg.Length + 2);
        sb.Append('"');
        for (var i = 0; i < arg.Length; i++)
        {
            var c = arg[i];
            if (c == '"') sb.Append("\\\"");
            else sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }
}

