import { defineStore } from 'pinia';
import { ref } from 'vue';
import { adminUserApi } from '@/api/adminUserApi';
import axiosClient from '@/api/axiosClient';

export const useAdminUserStore = defineStore('adminUsers', () => {
  const users = ref([]);
  const loading = ref(false);
  const departments = ref([]);
  const projectRoleAssignments = ref([]);
  const availableProjects = ref([]);
  const roles = ref([]);
  const permissions = ref([]);

  const fetchUsers = async (search = '') => {
    loading.value = true;
    try {
      const res = await adminUserApi.getUsers({ search });
      if (res.data && res.data.data) {
        users.value = res.data.data;
      }
    } catch (error) {
      console.error('Failed to load users:', error);
    } finally {
      loading.value = false;
    }
  };

  const suspendUser = async (userId) => {
    try {
      await adminUserApi.suspendUser(userId);
      const userIndex = users.value.findIndex(user => user.id === userId);
      if (userIndex !== -1) {
        users.value[userIndex].isActive = false;
        users.value[userIndex].status = 'Suspended';
      }
      return true;
    } catch (error) {
      console.error('Failed to suspend user:', error);
      throw error;
    }
  };

  const createUser = async (data) => {
    loading.value = true;
    try {
      await adminUserApi.createUser(data);
      await fetchUsers();
      return true;
    } catch (error) {
      console.error('Failed to create user:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  };

  const inviteUsers = async (payloads) => {
    loading.value = true;
    try {
      await Promise.all(payloads.map(payload => adminUserApi.createUser(payload)));
      await fetchUsers();
      return true;
    } catch (error) {
      console.error('Failed to invite users:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  };

  const removeUser = async (userId) => {
    loading.value = true;
    try {
      await adminUserApi.removeUser(userId);
      await fetchUsers();
      return true;
    } catch (error) {
      console.error('Failed to remove user:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  };

  const fetchDepartments = async () => {
    try {
      const res = await axiosClient.get('/admin/users/departments');
      departments.value = res.data?.data || [];
      return departments.value;
    } catch (error) {
      console.error('Failed to load departments:', error);
      departments.value = [];
      return departments.value;
    }
  };

  const createDepartment = async (payload) => {
    await axiosClient.post('/admin/users/departments', payload);
    return fetchDepartments();
  };

  const updateDepartment = async (departmentId, payload) => {
    await axiosClient.put(`/admin/users/departments/${departmentId}`, payload);
    return fetchDepartments();
  };

  const deleteDepartment = async (departmentId) => {
    await axiosClient.delete(`/admin/users/departments/${departmentId}`);
    return fetchDepartments();
  };

  const addDepartmentMember = async (departmentId, userId) => {
    await axiosClient.post(`/admin/users/departments/${departmentId}/members/${userId}`);
    await fetchUsers(); // Refresh users list so the frontend can see the updated departments property
    return true;
  };

  const removeDepartmentMember = async (departmentId, userId) => {
    await axiosClient.delete(`/admin/users/departments/${departmentId}/members/${userId}`);
    await fetchUsers(); // Refresh users list
    return true;
  };

  const fetchProjectRoleAssignments = async () => {
    try {
      const res = await axiosClient.get('/admin/users/project-role-assignments');
      projectRoleAssignments.value = res.data?.data || [];
      return projectRoleAssignments.value;
    } catch (error) {
      console.error('Failed to load project role assignments:', error);
      projectRoleAssignments.value = [];
      return projectRoleAssignments.value;
    }
  };

  const saveProjectRoleAssignment = async (payload) => {
    await axiosClient.put('/admin/users/project-role-assignments', payload);
    return fetchProjectRoleAssignments();
  };

  const deleteProjectRoleAssignment = async (payload) => {
    await axiosClient.delete('/admin/users/project-role-assignments', { data: payload });
    return fetchProjectRoleAssignments();
  };

  const fetchAccessibleProjects = async () => {
    try {
      const res = await axiosClient.get('/security/accessible-projects');
      availableProjects.value = res.data?.data?.items || [];
      return availableProjects.value;
    } catch (error) {
      console.error('Failed to load accessible projects:', error);
      availableProjects.value = [];
      return availableProjects.value;
    }
  };

  const fetchRoles = async (search = '') => {
    try {
      const res = await adminUserApi.getRoles({ search });
      roles.value = (res.data?.data?.roles || []).map(role => ({
        ...role,
        permissionIds: (role.permissions || []).map(permission => permission.id)
      }));
      permissions.value = res.data?.data?.permissions || [];
      return roles.value;
    } catch (error) {
      console.error('Failed to load roles:', error);
      roles.value = [];
      permissions.value = [];
      return roles.value;
    }
  };

  const createRole = async (payload) => {
    await adminUserApi.createRole(payload);
    return fetchRoles();
  };

  const updateRole = async (roleId, payload) => {
    await adminUserApi.updateRole(roleId, payload);
    return fetchRoles();
  };

  const deleteRole = async (roleId) => {
    await adminUserApi.deleteRole(roleId);
    return fetchRoles();
  };

  const assignUserRoles = async (userId, roleIds) => {
    await adminUserApi.assignUserRoles(userId, roleIds);
    await Promise.all([fetchUsers(), fetchRoles()]);
    return true;
  };

  return {
    users,
    loading,
    departments,
    projectRoleAssignments,
    availableProjects,
    roles,
    permissions,
    fetchUsers,
    suspendUser,
    createUser,
    inviteUsers,
    removeUser,
    fetchDepartments,
    createDepartment,
    updateDepartment,
    deleteDepartment,
    addDepartmentMember,
    removeDepartmentMember,
    fetchProjectRoleAssignments,
    saveProjectRoleAssignment,
    deleteProjectRoleAssignment,
    fetchAccessibleProjects,
    fetchRoles,
    createRole,
    updateRole,
    deleteRole,
    assignUserRoles
  };
});
