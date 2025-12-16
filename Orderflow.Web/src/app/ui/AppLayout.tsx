import { Outlet, Link } from "react-router-dom";
import { tokenStorage } from "../../lib/storage";

export function AppLayout() {
  const isLoggedIn = !!tokenStorage.get();

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b shadow-sm">
        <div className="mx-auto max-w-7xl px-4 py-4 flex items-center gap-4">
          <Link to="/" className="text-xl font-bold text-blue-600">
            OrderFlow
          </Link>

          <nav className="ml-auto flex items-center gap-6">
            <Link
              to="/"
              className="text-sm font-medium text-gray-700 hover:text-blue-600 transition"
            >
              Productos
            </Link>

            {isLoggedIn ? (
              <>
                <Link
                  to="/orders"
                  className="text-sm font-medium text-gray-700 hover:text-blue-600 transition"
                >
                  Mis Pedidos
                </Link>
                <Link
                  to="/profile"
                  className="text-sm font-medium text-gray-700 hover:text-blue-600 transition"
                >
                  Perfil
                </Link>
                <Link
                  to="/admin/users"
                  className="text-sm font-medium text-gray-700 hover:text-blue-600 transition"
                >
                  Admin
                </Link>
              </>
            ) : (
              <>
                <Link
                  to="/register"
                  className="text-sm font-medium text-gray-700 hover:text-blue-600 transition"
                >
                  Registrarse
                </Link>
                <Link
                  to="/login"
                  className="bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium py-2 px-4 rounded-md transition"
                >
                  Iniciar Sesión
                </Link>
              </>
            )}
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6">
        <Outlet />
      </main>

      <footer className="bg-white border-t mt-12">
        <div className="mx-auto max-w-7xl px-4 py-6 text-center text-sm text-gray-600">
          <p>&copy; 2024 OrderFlow. Sistema de gestión de pedidos e-commerce.</p>
        </div>
      </footer>
    </div>
  );
}
