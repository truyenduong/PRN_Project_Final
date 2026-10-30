import React, { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { ProjectService } from '../services/api';
import type { Project } from '../types';
import { X, FolderPlus, Loader2, AlertCircle, CheckCircle2 } from 'lucide-react';

interface CreateProjectModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (project: Project) => void;
}

export const CreateProjectModal: React.FC<CreateProjectModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
}) => {
  const { user, setCurrentRole } = useAuth();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [loading, setLoading] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('Please enter a project name');
      return;
    }

    if (!user?.id) {
      setError('Creator account information not found. Please log in again.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      // 1. Create project in DB
      // Backend creates record in project_members with role PROJECT_MANAGER
      setStatusMessage('Initializing project and granting Project Manager permissions...');
      const createdProject = await ProjectService.createProject({
        name: name.trim(),
        description: description.trim() || undefined,
        ownerId: user.id,
      });

      // 2. Switch workspace role in FE to PROJECT_MANAGER
      setCurrentRole('PROJECT_MANAGER');

      setStatusMessage('Project created successfully!');
      setTimeout(() => {
        setName('');
        setDescription('');
        setStatusMessage(null);
        setLoading(false);
        onSuccess(createdProject);
        onClose();
      }, 500);
    } catch (err: any) {
      console.error('Error creating project:', err);
      setError(err.message || 'Could not create project. Please try again!');
      setLoading(false);
      setStatusMessage(null);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm">
      <div className="bg-white rounded-3xl max-w-lg w-full p-7 sm:p-8 shadow-2xl relative border border-gray-100">
        <button
          onClick={onClose}
          disabled={loading}
          className="absolute top-6 right-6 text-gray-400 hover:text-gray-600 transition-colors cursor-pointer disabled:opacity-50"
        >
          <X size={20} />
        </button>

        <div className="flex items-center gap-3 mb-6">
          <div className="w-12 h-12 rounded-2xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
            <FolderPlus size={24} />
          </div>
          <div>
            <h2 className="text-xl font-bold text-gray-900">Create New Project</h2>
            <p className="text-xs text-gray-500 mt-0.5">
              You will be assigned as <span className="font-semibold text-emerald-700">Project Manager</span> for this project
            </p>
          </div>
        </div>

        {error && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 text-red-600 text-xs rounded-xl flex items-center gap-2">
            <AlertCircle size={16} className="shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {statusMessage && (
          <div className="mb-4 p-3 bg-emerald-50 border border-emerald-200 text-emerald-700 text-xs rounded-xl flex items-center gap-2">
            {loading ? (
              <Loader2 size={16} className="shrink-0 animate-spin" />
            ) : (
              <CheckCircle2 size={16} className="shrink-0 text-emerald-600" />
            )}
            <span>{statusMessage}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1.5">
              Project Name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Sprint Management System"
              className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800"
              disabled={loading}
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1.5">
              Project Description
            </label>
            <textarea
              rows={3}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Describe goals, scope, or details about the project..."
              className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 resize-none"
              disabled={loading}
            />
          </div>

          <div className="p-3.5 bg-gray-50 rounded-2xl border border-gray-100 text-xs text-gray-600 space-y-1">
            <div className="font-semibold text-gray-800 flex items-center gap-1.5">
              <span>✨ Privileges upon creation:</span>
            </div>
            <ul className="list-disc list-inside text-[11px] text-gray-500 space-y-0.5">
              <li>Automatically designated as <strong className="text-emerald-700">Project Manager</strong>.</li>
              <li>Create and manage <strong className="text-gray-700">Sprint cycles</strong>.</li>
              <li><strong className="text-gray-700">Add members</strong> and assign specific project roles.</li>
            </ul>
          </div>

          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              disabled={loading}
              className="px-4 py-2 text-xs font-semibold text-gray-600 hover:text-gray-800 hover:bg-gray-100 rounded-xl transition-all cursor-pointer disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !name.trim()}
              className="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-all cursor-pointer disabled:opacity-50"
            >
              {loading && <Loader2 size={14} className="animate-spin" />}
              {loading ? 'Initializing...' : 'Create Project & Start'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
