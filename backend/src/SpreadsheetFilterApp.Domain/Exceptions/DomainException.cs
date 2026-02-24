using System;

namespace SpreadsheetFilterApp.Domain.Exceptions;

public sealed class DomainException(string message) : Exception(message);
