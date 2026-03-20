import http from '../http'
import type {
  CreateGroupRequest,
  GetGroupsResponse,
  GetGroupMembersResponse,
  GroupDetailResponse,
  SendGroupMessageRequest,
  GetGroupMessagesResponse,
  AddGroupMemberRequest,
  UpdateGroupRequest,
  SearchGroupResponse,
  JoinGroupRequest,
  SendGroupJoinRequestRequest,
  GetGroupJoinRequestsResponse,
  ProcessGroupJoinRequestRequest,
} from '@/types'
import type { Group } from '@/types'

export const groupsApi = {
  // 创建群组
  async create(data: CreateGroupRequest): Promise<Group> {
    const response = await http.post<Group>('/api/groups', data)
    return response.data
  },

  // 获取用户加入的群组列表
  async getMyGroups(): Promise<GetGroupsResponse> {
    const response = await http.get<GetGroupsResponse>('/api/groups')
    return response.data
  },

  // 获取群组详情（含成员和公告）
  async getDetail(id: string): Promise<GroupDetailResponse> {
    const response = await http.get<GroupDetailResponse>(`/api/groups/${id}`)
    return response.data
  },

  // 获取群组（带 announcement 字段）
  async getById(id: string): Promise<Group> {
    const response = await http.get<GroupDetailResponse>(`/api/groups/${id}`)
    const detail = response.data
    return { ...detail.group, announcement: detail.announcement }
  },

  // 获取群组成员（通过群组详情接口）
  async getMembers(id: string): Promise<GetGroupMembersResponse> {
    const response = await http.get<GroupDetailResponse>(`/api/groups/${id}`)
    return { members: response.data.members }
  },

  // 获取群组消息 (v1.2.0 更新 - 游标分页)
  async getMessages(
    groupId: string,
    options?: {
      before?: string  // 消息 ID，获取此消息之前的消息（加载历史）
      after?: string   // 消息 ID，获取此消息之后的消息（加载新消息）
      limit?: number   // 每页数量，1-100，默认 50
    }
  ): Promise<GetGroupMessagesResponse> {
    const response = await http.get<GetGroupMessagesResponse>(
      `/api/messages/group/${groupId}`,
      {
        params: {
          before: options?.before,
          after: options?.after,
          limit: options?.limit || 50,
        },
      }
    )
    return response.data
  },

  // 发送群组消息 (v1.1.0 更新 - 通过 HTTP API)
  async sendMessage(groupId: string, data: SendGroupMessageRequest): Promise<void> {
    await http.post(`/api/messages/group/${groupId}/send`, data)
  },

  // 邀请成员
  async inviteMembers(id: string, data: AddGroupMemberRequest): Promise<void> {
    await http.post(`/api/groups/${id}/members`, data)
  },

  // 移除成员
  async removeMember(id: string, userId: string): Promise<void> {
    await http.delete(`/api/groups/${id}/members/${userId}`)
  },

  // 离开群组
  async leave(id: string): Promise<void> {
    await http.post(`/api/groups/${id}/leave`)
  },

  // 更新群组信息
  async update(id: string, data: UpdateGroupRequest): Promise<Group> {
    const response = await http.put<Group>(`/api/groups/${id}`, data)
    return response.data
  },

  // 设置群公告
  async setAnnouncement(id: string, content: string): Promise<void> {
    await http.put(`/api/groups/${id}/announcement`, { content })
  },

  // 清除群公告
  async clearAnnouncement(id: string): Promise<void> {
    await http.delete(`/api/groups/${id}/announcement`)
  },

  // 删除群组
  async delete(id: string): Promise<void> {
    await http.delete(`/api/groups/${id}`)
  },

  // 根据群号搜索群组
  async searchByNumber(groupNumber: number): Promise<SearchGroupResponse> {
    const response = await http.get<SearchGroupResponse>(`/api/groups/search/${groupNumber}`)
    return response.data
  },

  // 加入群组（仅公开群组）
  async join(groupNumber: number): Promise<void> {
    await http.post('/api/groups/join', { groupNumber })
  },

  // 发送入群申请（非公开群组）
  async sendJoinRequest(data: SendGroupJoinRequestRequest): Promise<void> {
    await http.post('/api/groups/join-request', data)
  },

  // 获取群组的入群申请列表（群主/管理员）
  async getGroupJoinRequests(groupId: string): Promise<GetGroupJoinRequestsResponse> {
    const response = await http.get<GetGroupJoinRequestsResponse>(`/api/groups/${groupId}/join-requests`)
    return response.data
  },

  // 获取我的入群申请列表
  async getMyJoinRequests(): Promise<GetGroupJoinRequestsResponse> {
    const response = await http.get<GetGroupJoinRequestsResponse>('/api/groups/my-join-requests')
    return response.data
  },

  // 处理入群申请（接受/拒绝）
  async processJoinRequest(requestId: string, data: ProcessGroupJoinRequestRequest): Promise<void> {
    await http.post(`/api/groups/join-requests/${requestId}/process`, data)
  },
}
