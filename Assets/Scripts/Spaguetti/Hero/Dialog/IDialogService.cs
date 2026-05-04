using Domain.Entities;
using Spaguetti.Hero;
using Hero = Domain.Entities.Hero;

public interface IDialogService
{
    void StartDialog(Hero hero, DialogData dialogs, string player, IProfilePictureFactory pictureFactory,
        IHeroController heroController);
}