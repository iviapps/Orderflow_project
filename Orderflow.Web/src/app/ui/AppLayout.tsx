import { Outlet, Link, useLocation } from "react-router-dom";
import { tokenStorage } from "../../lib/storage";

export function AppLayout() {
  const isLoggedIn = !!tokenStorage.get();
  const location = useLocation();

  const isActive = (path: string) => {
    return location.pathname === path;
  };

  const NavLink = ({ to, children, icon }: { to: string; children: React.ReactNode; icon?: React.ReactNode }) => {
    const active = isActive(to);
    return (
      <Link
        to={to}
        className={`flex items-center gap-2 text-sm font-medium transition-all duration-200 ${
          active
            ? "text-primary-600"
            : "text-gray-700 hover:text-primary-600"
        }`}
      >
        {icon}
        {children}
      </Link>
    );
  };

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header with gradient accent */}
      <header className="bg-white border-b shadow-sm sticky top-0 z-50 backdrop-blur-lg bg-white/95">
        <div className="h-1 bg-gradient-to-r from-primary-600 via-blue-500 to-primary-600"></div>
        <div className="mx-auto max-w-7xl px-4 py-4">
          <div className="flex items-center justify-between">
            {/* Logo */}
            <Link
              to="/"
              className="flex items-center gap-2 group"
            >
              <div className="bg-gradient-to-br from-primary-600 to-blue-600 p-2 rounded-lg shadow-md group-hover:shadow-lg transition-all duration-200 group-hover:scale-105">
                <svg
                  className="w-6 h-6 text-white"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"
                  />
                </svg>
              </div>
              <span className="text-xl font-bold bg-gradient-to-r from-primary-600 to-blue-600 bg-clip-text text-transparent">
                OrderFlow
              </span>
            </Link>

            {/* Navigation */}
            <nav className="hidden md:flex items-center gap-8">
              <NavLink
                to="/"
                icon={
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
                  </svg>
                }
              >
                Productos
              </NavLink>

              {isLoggedIn ? (
                <>
                  <NavLink
                    to="/orders"
                    icon={
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                      </svg>
                    }
                  >
                    Mis Pedidos
                  </NavLink>
                  <NavLink
                    to="/profile"
                    icon={
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                    }
                  >
                    Perfil
                  </NavLink>
                  <NavLink
                    to="/admin/users"
                    icon={
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                      </svg>
                    }
                  >
                    Admin
                  </NavLink>
                </>
              ) : (
                <>
                  <NavLink to="/register">
                    Registrarse
                  </NavLink>
                  <Link
                    to="/login"
                    className="btn-primary btn-md shadow-md hover:shadow-lg"
                  >
                    Iniciar Sesión
                  </Link>
                </>
              )}
            </nav>
          </div>
        </div>
      </header>

      {/* Main content */}
      <main className="flex-1 mx-auto max-w-7xl w-full px-4 py-8">
        <Outlet />
      </main>

      {/* Footer */}
      <footer className="bg-white border-t mt-auto">
        <div className="mx-auto max-w-7xl px-4 py-8">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mb-6">
            {/* Brand */}
            <div>
              <div className="flex items-center gap-2 mb-3">
                <div className="bg-gradient-to-br from-primary-600 to-blue-600 p-1.5 rounded-lg">
                  <svg
                    className="w-5 h-5 text-white"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"
                    />
                  </svg>
                </div>
                <span className="font-bold text-lg text-gradient">OrderFlow</span>
              </div>
              <p className="text-sm text-gray-600">
                Sistema de gestión de pedidos e-commerce moderno y eficiente.
              </p>
            </div>

            {/* Quick Links */}
            <div>
              <h3 className="font-semibold text-gray-900 mb-3">Enlaces Rápidos</h3>
              <ul className="space-y-2 text-sm">
                <li>
                  <Link to="/" className="text-gray-600 hover:text-primary-600 transition">
                    Productos
                  </Link>
                </li>
                <li>
                  <Link to="/orders" className="text-gray-600 hover:text-primary-600 transition">
                    Pedidos
                  </Link>
                </li>
                <li>
                  <Link to="/profile" className="text-gray-600 hover:text-primary-600 transition">
                    Perfil
                  </Link>
                </li>
              </ul>
            </div>

            {/* Contact */}
            <div>
              <h3 className="font-semibold text-gray-900 mb-3">Contacto</h3>
              <ul className="space-y-2 text-sm text-gray-600">
                <li className="flex items-center gap-2">
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                  </svg>
                  info@orderflow.com
                </li>
                <li className="flex items-center gap-2">
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                  </svg>
                  +54 11 1234-5678
                </li>
              </ul>
            </div>
          </div>

          <div className="border-t pt-6">
            <p className="text-center text-sm text-gray-600">
              &copy; {new Date().getFullYear()} OrderFlow. Todos los derechos reservados.
            </p>
          </div>
        </div>
      </footer>
    </div>
  );
}
