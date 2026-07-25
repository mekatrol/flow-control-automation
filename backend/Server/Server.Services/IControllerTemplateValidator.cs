using Server.Services.Contracts;

namespace Server.Services;

public interface IControllerTemplateValidator
{
    ValidatedControllerTemplate Validate(
        ControllerTemplate template,
        bool allowBuiltInDefault = false);
}