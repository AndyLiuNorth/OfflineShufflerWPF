using ShufflerWPF.Manager;
using ShufflerWPF.Model;

namespace ShufflerWPF.IntegrationTests;

[Collection("TrueBox")]
public class DoTrueBoxActionManagerTests
{
   [Fact]
   public async Task DoAction_0_Normal201_TrueId()
   {
      
      await TrueBoxTestHost.EnsureStartedAsync();
      
      // Arrange
      var vm = new CreateIDPageViewModel
      {
         SelectedColorType = "Red",
         SelectedIsMain = "Regular",
         SelectedTableId = "DEMO-01",
         maxCount = 5
      };
      
      // Act
      var (box, error) = await DoTrueBoxActionManager.DoTrueBoxActionZeroAsync(vm);
      
      // Assert
      Assert.Equal(string.Empty, error);
      Assert.NotNull(box);
      Assert.False(string.IsNullOrWhiteSpace(box?.trueId));
   }
}