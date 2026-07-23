namespace CB2Toolkit.Core.Models;

public record CommandResult(bool IsSuccess, string? Message = null)
{
    public static CommandResult Success(string? msg = null) => new(true, msg);
    public static CommandResult Fail(string errorMsg) => new(false, errorMsg);
}