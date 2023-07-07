using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataTables.GeneratorCore;

internal class ILogger : IDisposable
{
    private readonly Action<string> m_Logger;
    private readonly StringBuilder m_StringBuilder;

    private bool m_HasError;

    public ILogger(Action<string> logger)
    {
        m_Logger = logger;

        m_StringBuilder = new();
        m_HasError = false;
    }

    public void Debug(string message)
    {
        m_StringBuilder.AppendLine(message);
    }

    public void Debug(string message, params object[] args)
    {
        m_StringBuilder.AppendFormat(message, args);
        m_StringBuilder.AppendLine();
    }

    public void Error(string message)
    {
        m_HasError = true;
        m_StringBuilder.AppendLine(message);
    }

    public void Error(string message, params object[] args)
    {
        m_HasError = true;
        m_StringBuilder.AppendFormat(message, args);
        m_StringBuilder.AppendLine();
    }

    public void Dispose()
    {
        if (m_HasError)
            Console.ForegroundColor = ConsoleColor.Red;

        m_Logger(m_StringBuilder.ToString());

        if (m_HasError)
            Console.ResetColor();
    }
}
