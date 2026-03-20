using FluentAssertions;
using IMSharp.Core.DTOs;
using IMSharp.Core.Services;
using IMSharp.Domain.Entities;
using IMSharp.Domain.Exceptions;
using IMSharp.Infrastructure.Data;
using IMSharp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IMSharp.Tests;

public class CoreServiceTests
{
    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string>? settings = null)
    {
        var configSettings = settings ?? new Dictionary<string, string>
        {
            ["GroupLimits:MaxGroupsPerUser"] = "10",
            ["GroupLimits:GroupNumberMin"] = "10000000",
            ["GroupLimits:GroupNumberMax"] = "99999999",
            ["GroupLimits:MaxRetryAttempts"] = "10"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configSettings)
            .Build();
    }

    [Fact]
    public async Task SendGroupMessage_ReturnsSentMessage()
    {
        await using var context = CreateContext(nameof(SendGroupMessage_ReturnsSentMessage));

        var owner = new User
        {
            Username = "owner",
            DisplayName = "owner",
            OAuthProvider = "Generic",
            OAuthId = "owner-oauth"
        };

        var group = new Group
        {
            Name = "group",
            OwnerId = owner.Id,
            IsPublic = true
        };

        var ownerMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = owner.Id,
            Role = GroupMemberRole.Owner
        };

        context.Users.Add(owner);
        context.Groups.Add(group);
        context.GroupMembers.Add(ownerMember);
        await context.SaveChangesAsync();

        var configuration = CreateConfiguration();
        var groupRepository = new GroupRepository(context);
        var friendRepository = new FriendRepository(context);
        var groupService = new GroupService(groupRepository, friendRepository, configuration);

        var message = await groupService.SendMessageAsync(
            owner.Id,
            group.Id,
            new SendGroupMessageRequest("hello", MessageType.Text, null));

        var saved = await groupRepository.GetMessageByIdAsync(message.Id);
        saved.Should().NotBeNull();
        saved.Content.Should().Be("hello");
        message.Id.Should().Be(saved.Id);
    }

    [Fact]
    public async Task NewGroupEntities_HaveNullableUpdatedAt()
    {
        await using var context = CreateContext(nameof(NewGroupEntities_HaveNullableUpdatedAt));

        var user = new User
        {
            Username = "user",
            DisplayName = "user",
            OAuthProvider = "Generic",
            OAuthId = "user-oauth"
        };

        var group = new Group
        {
            Name = "group",
            OwnerId = user.Id,
            IsPublic = true
        };

        var member = new GroupMember
        {
            GroupId = group.Id,
            UserId = user.Id,
            Role = GroupMemberRole.Member
        };

        var message = new GroupMessage
        {
            GroupId = group.Id,
            SenderId = user.Id,
            Content = "content",
            Type = MessageType.Text
        };

        var joinRequest = new GroupJoinRequest
        {
            GroupId = group.Id,
            UserId = user.Id,
            Status = GroupJoinRequestStatus.Pending
        };

        context.Users.Add(user);
        context.Groups.Add(group);
        context.GroupMembers.Add(member);
        context.GroupMessages.Add(message);
        context.GroupJoinRequests.Add(joinRequest);
        await context.SaveChangesAsync();

        group.UpdatedAt.Should().BeNull();
        member.UpdatedAt.Should().BeNull();
        message.UpdatedAt.Should().BeNull();
        joinRequest.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task MarkAsRead_ReturnsSenderAndUpdatesStatus()
    {
        await using var context = CreateContext(nameof(MarkAsRead_ReturnsSenderAndUpdatesStatus));

        var sender = new User
        {
            Username = "sender",
            DisplayName = "sender",
            OAuthProvider = "Generic",
            OAuthId = "sender-oauth"
        };

        var receiver = new User
        {
            Username = "receiver",
            DisplayName = "receiver",
            OAuthProvider = "Generic",
            OAuthId = "receiver-oauth"
        };

        var message = new PrivateMessage
        {
            SenderId = sender.Id,
            ReceiverId = receiver.Id,
            Content = "hi",
            Type = MessageType.Text,
            Status = MessageStatus.Sent
        };

        context.Users.AddRange(sender, receiver);
        context.PrivateMessages.Add(message);
        await context.SaveChangesAsync();

        var messageRepository = new PrivateMessageRepository(context);
        var userRepository = new UserRepository(context);
        var friendRepository = new FriendRepository(context);
        var service = new PrivateMessageService(messageRepository, userRepository, friendRepository);

        var (senderId, readAt) = await service.MarkAsReadAndGetSenderAsync(receiver.Id, message.Id);
        senderId.Should().Be(sender.Id);
        readAt.Should().NotBe(default);

        var updated = await messageRepository.GetByIdAsync(message.Id);
        updated!.Status.Should().Be(MessageStatus.Read);
    }

    [Fact]
    public async Task AcceptFriendRequest_CreatesBidirectionalFriendship()
    {
        await using var context = CreateContext(nameof(AcceptFriendRequest_CreatesBidirectionalFriendship));

        var sender = new User
        {
            Username = "sender",
            DisplayName = "sender",
            OAuthProvider = "Generic",
            OAuthId = "sender-oauth"
        };

        var receiver = new User
        {
            Username = "receiver",
            DisplayName = "receiver",
            OAuthProvider = "Generic",
            OAuthId = "receiver-oauth"
        };

        var request = new FriendRequest
        {
            SenderId = sender.Id,
            ReceiverId = receiver.Id,
            Status = FriendRequestStatus.Pending
        };

        context.Users.AddRange(sender, receiver);
        context.FriendRequests.Add(request);
        await context.SaveChangesAsync();

        var friendRepository = new FriendRepository(context);
        var userRepository = new UserRepository(context);
        var messageRepository = new PrivateMessageRepository(context);
        var service = new FriendService(friendRepository, userRepository,messageRepository, context);

        await service.ProcessFriendRequestAsync(receiver.Id, request.Id, new ProcessFriendRequestRequest(true));

        var friendships = await friendRepository.GetFriendshipsAsync(sender.Id);
        friendships.Should().ContainSingle(f => f.FriendId == receiver.Id);

        var reverse = await friendRepository.GetFriendshipsAsync(receiver.Id);
        reverse.Should().ContainSingle(f => f.FriendId == sender.Id);
    }

    [Fact]
    public async Task CreateGroup_AutoGeneratesGroupNumber()
    {
        await using var context = CreateContext(nameof(CreateGroup_AutoGeneratesGroupNumber));

        var owner = new User
        {
            Username = "owner",
            DisplayName = "Owner",
            OAuthProvider = "Generic",
            OAuthId = "owner-oauth"
        };

        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var configuration = CreateConfiguration();
        var groupRepository = new GroupRepository(context);
        var friendRepository = new FriendRepository(context);
        var groupService = new GroupService(groupRepository, friendRepository, configuration);

        var request = new CreateGroupRequest("Test Group", null, null, null, true);
        var result = await groupService.CreateGroupAsync(owner.Id, request);

        result.GroupNumber.Should().BeInRange(10000000, 99999999);
    }

    [Fact]
    public async Task CreateGroup_EnforcesMaxGroupsPerUser()
    {
        await using var context = CreateContext(nameof(CreateGroup_EnforcesMaxGroupsPerUser));

        var owner = new User
        {
            Username = "owner",
            DisplayName = "Owner",
            OAuthProvider = "Generic",
            OAuthId = "owner-oauth"
        };

        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["GroupLimits:MaxGroupsPerUser"] = "3",
            ["GroupLimits:GroupNumberMin"] = "10000000",
            ["GroupLimits:GroupNumberMax"] = "99999999",
            ["GroupLimits:MaxRetryAttempts"] = "10"
        });

        var groupRepository = new GroupRepository(context);
        var friendRepository = new FriendRepository(context);
        var groupService = new GroupService(groupRepository, friendRepository, configuration);

        // 创建 3 个群组
        for (int i = 0; i < 3; i++)
        {
            var request = new CreateGroupRequest($"Group {i}", null, null, null, true);
            await groupService.CreateGroupAsync(owner.Id, request);
        }

        // 尝试创建第 4 个
        var failRequest = new CreateGroupRequest("Group 4", null, null, null, true);
        var act = async () => await groupService.CreateGroupAsync(owner.Id, failRequest);

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("您最多只能创建 3 个群组");
    }
}
