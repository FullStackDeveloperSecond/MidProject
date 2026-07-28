namespace MidProject.Models;

public class MemberAvatarFrame
{
    public int MemberAvatarFrameID { get; set; }
    public int MemberID { get; set; }
    public int FrameID { get; set; }
    public DateTime RedeemedAt { get; set; } = DateTime.Now;

    public Member? Member { get; set; }
    public AvatarFrame? Frame { get; set; }
}
