using ApplicationInterface.DataAccess;
using ApplicationInterface.Repositories;
using Autofac;
using Autofac.Integration.Mvc;
using FireData.Repositories;
using FireWorkforceManagement.Repositories;
using System.Web.Mvc;

namespace FireAttendance {
    public static class Bootstrapper {
        public static void Initialize() {
            var builder = new ContainerBuilder();
            builder.RegisterControllers(typeof(MvcApplication).Assembly);
            IActiveDirectoryDa adda;
            ISecurityDa secda;
            if (MvcApplication.isDebugMode) {
                adda = new FakeActiveDirectoryDa();
                secda = new FakeSecurityDa();
            }
            else {
                adda = new ActiveDirectoryDa();
                secda = new SecurityDa();
            }
            builder.RegisterInstance<IUserRepository>(new UserRepository(adda, secda));
            builder.RegisterInstance<ISecRepository>(new SecRepository(secda));
            IConfigRepository configRepo = new ConfigRepository();
            builder.RegisterInstance(configRepo);
            IFireFightersRepository ffRepo = new FireFightersRepository(configRepo);
            builder.RegisterInstance(ffRepo);
            builder.RegisterInstance<IDailyRosterRepository>(new DailyRosterRepository(configRepo, ffRepo));
            builder.RegisterInstance<IWorkHourRepository>(new WorkHourRepository(configRepo, ffRepo));
            builder.RegisterInstance<ITimeOwingRepository>(new TimeOwingRepository());
            builder.RegisterInstance<IOverTimeRepository>(new OverTimeRepository());

            var container = builder.Build();
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));
        }
    }
}