import axiosClient from './axiosClient';

export const adminUserApi = {
  getUsers(params) {
    return axiosClient.get('/admin/users', { params });
  },
  suspendUser(userId) {
    return axiosClient.put(`/admin/users/${userId}/suspend`);
  },
  createUser(data) {
    return axiosClient.post('/admin/users', data);
  },
  getRoles(params) {
    return axiosClient.get('/admin/users/roles', { params });
  },
  getPermissionCatalog() {
    return axiosClient.get('/permissions/catalog');
  },
  createRole(data) {
    return axiosClient.post('/admin/users/roles', data);
  },
  updateRole(roleId, data) {
    return axiosClient.put(`/admin/users/roles/${roleId}`, data);
  },
  deleteRole(roleId) {
    return axiosClient.delete(`/admin/users/roles/${roleId}`);
  },
  assignUserRoles(userId, roleIds) {
    return axiosClient.post(`/admin/users/${userId}/roles`, { roleIds });
  },
  removeUser(userId) {
    return axiosClient.delete(`/admin/users/${userId}`);
  }
};
