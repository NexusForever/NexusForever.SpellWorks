using NexusForever.SpellWorks.Services;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Un-maximizing by dragging the title bar. The window has to come back under the pointer; a plain
    /// restore puts it back where it was before maximizing, leaving the cursor holding empty desktop.
    /// </summary>
    public class RestoreOnDragTests
    {
        // A window maximized across a 1920-wide monitor, restoring to 1000 wide.
        private static (double Left, double Top) Restore(double grabX, double grabY = 12) =>
            RestoreOnDrag.Origin(windowOrigin: (0, 0), grab: (grabX, grabY),
                maximisedWidth: 1920, restoredWidth: 1000);

        [Fact]
        public void The_cursor_keeps_its_place_along_the_title_bar()
        {
            // Grabbed at the middle of the maximized bar, so it stays at the middle of the restored one.
            (double left, _) = Restore(960);

            Assert.Equal(960 - 500, left);
        }

        [Fact]
        public void Grabbing_the_far_left_keeps_the_window_under_the_cursor()
        {
            (double left, _) = Restore(0);

            Assert.Equal(0, left);
        }

        [Fact]
        public void Grabbing_the_far_right_pulls_the_whole_window_across()
        {
            // The restored window is narrower, so its left edge has to move a long way right.
            (double left, _) = Restore(1920);

            Assert.Equal(1920 - 1000, left);
        }

        [Fact]
        public void The_grab_point_always_lands_inside_the_restored_window()
        {
            foreach (double grabX in new double[] { 0, 1, 250, 960, 1500, 1919, 1920 })
            {
                (double left, _) = Restore(grabX);

                Assert.InRange(grabX - left, 0, 1000);
            }
        }

        [Fact]
        public void The_window_keeps_its_top_so_the_pointer_stays_on_the_title_bar()
        {
            (_, double top) = RestoreOnDrag.Origin((0, 40), (960, 12), 1920, 1000);

            Assert.Equal(40, top);
        }

        [Fact]
        public void A_window_on_a_second_monitor_restores_beside_the_cursor_there()
        {
            // The origin is in virtual screen space, so a monitor to the right carries a large offset.
            (double left, double top) = RestoreOnDrag.Origin((1920, 0), (960, 12), 1920, 1000);

            Assert.Equal(1920 + 960 - 500, left);
            Assert.Equal(0, top);
        }

        [Fact]
        public void A_grab_beyond_the_window_is_treated_as_its_edge()
        {
            // The pointer can be reported just outside the window between the press and this running.
            (double leftOfLeft, _) = Restore(-30);
            (double rightOfRight, _) = Restore(2200);

            Assert.Equal(-30, leftOfLeft);
            Assert.Equal(2200 - 1000, rightOfRight);
        }

        [Fact]
        public void A_window_with_no_width_centres_on_the_cursor()
        {
            (double left, _) = RestoreOnDrag.Origin((0, 0), (400, 12), maximisedWidth: 0, restoredWidth: 1000);

            Assert.Equal(400 - 500, left);
        }
    }
}
