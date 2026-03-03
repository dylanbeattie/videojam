namespace VideoJam.Services;

/// <summary>
/// Thrown when a <c>.show</c> file fails validation or cannot be parsed.
/// Callers may catch this type specifically to distinguish show-file problems
/// from unrelated runtime exceptions.
/// </summary>
public sealed class ShowFileException : Exception {
	/// <summary>
	/// Initialises a new <see cref="ShowFileException"/> with the specified message.
	/// </summary>
	/// <param name="message">Human-readable description of the validation failure.</param>
	public ShowFileException(string message) : base(message) { }
}
