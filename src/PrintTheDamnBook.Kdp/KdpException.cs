namespace PrintTheDamnBook.Kdp;

/// <summary>Why a KDP interior could not be made; the message is meant for the person running the command.</summary>
public sealed class KdpException(string message, Exception? inner = null) : Exception(message, inner);
