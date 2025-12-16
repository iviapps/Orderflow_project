import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../../../lib/api";
import { tokenStorage } from "../../../lib/storage";

interface UserProfile {
  userId: string;
  email: string;
  userName: string;
  emailConfirmed: boolean;
  phoneNumber: string | null;
  roles: string[];
}

export function ProfilePage() {
  const navigate = useNavigate();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const token = tokenStorage.get();
    if (!token) {
      navigate("/login");
      return;
    }

    // Configurar el header de autorización
    api.defaults.headers.common["Authorization"] = `Bearer ${token}`;

    api
      .get("/api/v1/users/me")
      .then((response) => {
        setProfile(response.data);
        setLoading(false);
      })
      .catch((err) => {
        if (err.response?.status === 401) {
          tokenStorage.clear();
          navigate("/login");
        } else {
          setError(
            err.response?.data?.message || "Error al cargar el perfil"
          );
          setLoading(false);
        }
      });
  }, [navigate]);

  const handleLogout = () => {
    tokenStorage.clear();
    delete api.defaults.headers.common["Authorization"];
    navigate("/login");
  };

  if (loading) {
    return (
      <div className="flex justify-center items-center min-h-[400px]">
        <div className="text-gray-500">Cargando perfil...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-4xl mx-auto mt-8">
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
          {error}
        </div>
      </div>
    );
  }

  if (!profile) {
    return null;
  }

  return (
    <div className="max-w-4xl mx-auto mt-8">
      <div className="bg-white shadow-md rounded-lg overflow-hidden">
        <div className="bg-gradient-to-r from-blue-500 to-blue-600 px-6 py-8">
          <h1 className="text-3xl font-bold text-white">Mi Perfil</h1>
        </div>

        <div className="p-6">
          <div className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Email
                </label>
                <div className="flex items-center gap-2">
                  <span className="text-gray-900">{profile.email}</span>
                  {profile.emailConfirmed ? (
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-green-100 text-green-800">
                      Verificado
                    </span>
                  ) : (
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-yellow-100 text-yellow-800">
                      No verificado
                    </span>
                  )}
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Nombre de Usuario
                </label>
                <span className="text-gray-900">{profile.userName}</span>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Teléfono
                </label>
                <span className="text-gray-900">
                  {profile.phoneNumber || "No especificado"}
                </span>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Roles
                </label>
                <div className="flex gap-2">
                  {profile.roles.map((role) => (
                    <span
                      key={role}
                      className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800"
                    >
                      {role}
                    </span>
                  ))}
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  ID de Usuario
                </label>
                <span className="text-gray-500 text-sm font-mono">
                  {profile.userId}
                </span>
              </div>
            </div>

            <div className="border-t border-gray-200 pt-6 mt-6">
              <div className="flex gap-4">
                <button className="bg-blue-600 hover:bg-blue-700 text-white font-medium py-2 px-4 rounded-md transition">
                  Editar Perfil
                </button>
                <button className="bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium py-2 px-4 rounded-md transition">
                  Cambiar Contraseña
                </button>
                <button
                  onClick={handleLogout}
                  className="ml-auto bg-red-50 hover:bg-red-100 text-red-700 font-medium py-2 px-4 rounded-md transition"
                >
                  Cerrar Sesión
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
