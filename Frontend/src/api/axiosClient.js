import axios from 'axios';
import { clearAuthSession, getCurrentAccessToken, updateCurrentAccessToken, waitForAuthReady } from '@/utils/authSession'
import { ensureAiOperationId } from '@/utils/aiOperationId'
import { applyAuthHeader, shouldRefreshUnauthorized } from '@/utils/authRequest'
import { attachCurrentAccessToken, createRefreshCoordinator } from '@/utils/authTransport'

const baseURL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api';

const axiosClient = axios.create({
    baseURL: baseURL,
    headers: {
        'Content-Type': 'application/json'
    },
    withCredentials: true // Rất quan trọng để đính kèm HttpOnly cookies (RefreshToken)
});

const refreshCoordinator = createRefreshCoordinator({
    refreshAccessToken: async () => {
        // The refresh cookie is sent automatically. The current access token identifies the session.
        const accessToken = getCurrentAccessToken();
        const authHeaders = accessToken ? { Authorization: `Bearer ${accessToken}` } : {};
        const { data } = await axios.post(`${baseURL}/auth/refresh-token`, {}, {
            headers: authHeaders,
            withCredentials: true
        });
        return data?.data?.accessToken ?? data?.accessToken;
    },
    updateAccessToken: updateCurrentAccessToken,
    handleRefreshFailure: () => {
        clearAuthSession();
        const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
        const redirect = currentPath && !currentPath.startsWith('/login')
            ? `?redirect=${encodeURIComponent(currentPath)}`
            : '';
        window.location.href = `/login${redirect}`;
    }
});

axiosClient.interceptors.request.use(
    async (config) => {
        ensureAiOperationId(config)
        await attachCurrentAccessToken(config, {
            waitForAuthReady,
            getCurrentAccessToken,
            applyAuthHeader
        })
        const locale = localStorage.getItem('admin_locale') || 'vi';
        config.headers['Accept-Language'] = locale;
        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);

axiosClient.interceptors.response.use(
    (response) => {
        return response;
    },
    async (error) => {
        const originalRequest = error.config;
        if (!originalRequest) {
            return Promise.reject(error);
        }
        if (shouldRefreshUnauthorized(error, originalRequest)) {
            if (!getCurrentAccessToken()) return Promise.reject(error);
            originalRequest._retry = true;
            try {
                return refreshCoordinator.retryAfterRefresh(token => {
                    applyAuthHeader(originalRequest, token)
                    return axiosClient(originalRequest)
                });
            } catch (err) {
                return Promise.reject(err);
            }
        }

        if (error.response && error.response.status === 409) {
            console.warn('Conflict detected:', error.response.data);
            // Optionally, we could emit a global event or show a notification
            // but for now, we'll let the component handle the catch block
        }

        return Promise.reject(error);
    }
);

export default axiosClient;
