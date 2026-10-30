import React, { useState, useEffect } from 'react';
import { RoleService, UserService, ProjectMemberService } from '../services/api';
import type { Role, User, ProjectMember } from '../types';
import { X, UserPlus, Loader2, AlertCircle, ShieldAlert } from 'lucide-react';

interface AddProjectMemberModalProps {
  projectId: number;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (member: ProjectMember) => void;
  existingMemberUserIds?: number[];
}

export const AddProjectMemberModal: React.FC<AddProjectMemberModalProps> = ({
  projectId,
  isOpen,
  onClose,
  onSuccess,
  existingMemberUserIds = [],
}) => {
  const [users, setUsers] = useState<User[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [selectedUserId, setSelectedUserId] = useState<number | ''>('');
  const [selectedRoleId, setSelectedRoleId] = useState<number | ''>('');
  
  const [loadingData, setLoadingData] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      loadUsersAndRoles();
    }
  }, [isOpen]);

  const loadUsersAndRoles = async () => {
    setLoadingData(true);
    setError(null);
    try {
      const [fetchedUsers, fetchedRoles] = await Promise.all([
        UserService.getUsers(),
        RoleService.getRoles(),
      ]);

      // Lọc danh sách roles: ẨN 2 ROLE 'USER' VÀ 'ADMIN' THEO YÊU CẦU NGHIỆP VỤ
      const filteredRoles = RoleService.filterAssignableRoles(fetchedRoles);
      setRoles(filteredRoles);

      // Lọc danh sách users chưa tham gia dự án
      const availableUsers = fetchedUsers.filter(
        (u) => !existingMemberUserIds.includes(u.id)
      );
      setUsers(availableUsers);

      if (availableUsers.length > 0) {
        setSelectedUserId(availableUsers[0].id);
      } else {
        setSelectedUserId('');
      }

      if (filteredRoles.length > 0) {
        // Mặc định chọn DEVELOPER nếu có, hoặc role đầu tiên
        const devRole = filteredRoles.find(
          (r) => (r.name || '').toUpperCase() === 'DEVELOPER'
        );
        setSelectedRoleId(devRole ? devRole.id : filteredRoles[0].id);
      } else {
        setSelectedRoleId('');
      }
    } catch (err: any) {
      console.error('Error loading users/roles:', err);
      setError('Could not load user accounts or roles from server.');
    } finally {
      setLoadingData(false);
    }
  };

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedUserId) {
      setError('Please select a member to add');
      return;
    }
    if (!selectedRoleId) {
      setError('Please select a project role');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const newMember = await ProjectMemberService.addMember({
        projectId,
        userId: Number(selectedUserId),
        roleId: Number(selectedRoleId),
      });

      setSubmitting(false);
      onSuccess(newMember);
      onClose();
    } catch (err: any) {
      console.error('Error adding member:', err);
      setError(err.message || 'Could not add member to project. Please try again!');
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm">
      <div className="bg-white rounded-3xl max-w-lg w-full p-7 sm:p-8 shadow-2xl relative border border-gray-100">
        <button
          onClick={onClose}
          disabled={submitting}
          className="absolute top-6 right-6 text-gray-400 hover:text-gray-600 transition-colors cursor-pointer disabled:opacity-50"
        >
          <X size={20} />
        </button>

        <div className="flex items-center gap-3 mb-6">
          <div className="w-12 h-12 rounded-2xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
            <UserPlus size={24} />
          </div>
          <div>
            <h2 className="text-xl font-bold text-gray-900">Add Member to Project</h2>
            <p className="text-xs text-gray-500 mt-0.5">
              Assign appropriate project role to team members
            </p>
          </div>
        </div>

        {error && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 text-red-600 text-xs rounded-xl flex items-center gap-2">
            <AlertCircle size={16} className="shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {loadingData ? (
          <div className="py-12 flex flex-col items-center justify-center gap-2 text-gray-400 text-xs">
            <Loader2 size={24} className="animate-spin text-emerald-600" />
            <span>Loading user accounts and project roles...</span>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-gray-700 mb-1.5">
                Select Member <span className="text-red-500">*</span>
              </label>
              {users.length === 0 ? (
                <div className="p-3 bg-gray-50 border border-gray-200 rounded-xl text-xs text-gray-500">
                  All available users are already members of this project.
                </div>
              ) : (
                <select
                  required
                  value={selectedUserId}
                  onChange={(e) => setSelectedUserId(Number(e.target.value))}
                  className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 cursor-pointer"
                  disabled={submitting}
                >
                  {users.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name} ({u.email})
                    </option>
                  ))}
                </select>
              )}
            </div>

            <div>
              <div className="flex items-center justify-between mb-1.5">
                <label className="text-xs font-semibold text-gray-700">
                  Assign Project Role <span className="text-red-500">*</span>
                </label>
                <span className="text-[10px] text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full font-medium">
                  Admin &amp; User roles hidden
                </span>
              </div>

              {roles.length === 0 ? (
                <div className="p-3 bg-amber-50 border border-amber-200 text-amber-800 text-xs rounded-xl flex items-center gap-2">
                  <ShieldAlert size={16} className="shrink-0" />
                  <span>No assignable project roles found.</span>
                </div>
              ) : (
                <select
                  required
                  value={selectedRoleId}
                  onChange={(e) => setSelectedRoleId(Number(e.target.value))}
                  className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 cursor-pointer"
                  disabled={submitting}
                >
                  {roles.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                    </option>
                  ))}
                </select>
              )}
            </div>

            <div className="p-3 bg-emerald-50/70 border border-emerald-100 rounded-2xl text-[11px] text-emerald-800 leading-relaxed">
              💡 <strong>Note:</strong> System roles <code>ADMIN</code> and <code>USER</code> are automatically excluded to preserve project permission boundaries.
            </div>

            <div className="flex items-center justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={onClose}
                disabled={submitting}
                className="px-4 py-2 text-xs font-semibold text-gray-600 hover:text-gray-800 hover:bg-gray-100 rounded-xl transition-all cursor-pointer disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={submitting || !selectedUserId || !selectedRoleId}
                className="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-all cursor-pointer disabled:opacity-50"
              >
                {submitting && <Loader2 size={14} className="animate-spin" />}
                {submitting ? 'Adding...' : 'Add to Project'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
};
