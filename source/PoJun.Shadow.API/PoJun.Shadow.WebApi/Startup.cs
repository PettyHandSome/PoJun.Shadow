using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PoJun.MongoDB.Repository;
using PoJun.Shadow.BaseFramework;
using PoJun.Shadow.Tools;
using PoJun.Shadow.WebApi.Filters;
using PoJun.Shadow.WebApi.Jobs;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

namespace PoJun.Shadow.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services"></param>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            //ע��MongoDB�ִ���������ÿ���ע�͵���
            RepositoryContainer.RegisterAll(AutofacModuleRegister.GetAllAssembliesName());            
            services.AddControllers();
            services.AddMvc(option =>
            {
                option.Filters.Add(typeof(ExceptionLogAttribute));
                option.Filters.Add(typeof(RequestLogAttribute));
                option.Filters.Add(typeof(ResponseLogAttribute));
                option.MaxModelValidationErrors = 100;
            })
            .AddNewtonsoftJson(option =>
            {
                option.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
                //����ѭ������
                option.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                //��ʹ���շ���ʽ��key
                option.SerializerSettings.ContractResolver = new DefaultContractResolver();
                //���Ӳ����Զ�ȥ��ǰ��ո�ת����
                option.SerializerSettings.Converters.Add(new TrimmingConverter());
            });
            //�������������ÿ���ע�͵���
            services.AddCors(options =>
            {
                options.AddPolicy("EnableCrossDomain", builder =>
                {
                    //builder.AllowAnyOrigin()//�����κ���Դ����������
                    builder.WithOrigins(APIConfig.GetInstance().RequestSource)                    
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();//ָ������cookie
                });
            });
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddApiVersioning(options =>
            {
                //������Ϊ true ʱ, API ��������Ӧ��ͷ��֧�ֵİ汾��Ϣ
                options.ReportApiVersions = true;
                //��ѡ����ڲ��ṩ�汾������Ĭ�������, �ٶ��� API �汾Ϊ1.0��
                options.AssumeDefaultVersionWhenUnspecified = true;
                //��ѡ������ָ����������δָ���汾ʱҪʹ�õ�Ĭ�� API �汾���⽫Ĭ�ϰ汾Ϊ1.0��
                options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
            });            
            //ע��Ȩ����֤
            services.AddScoped<AuthenticationAttribute>();

            //ע�� Quartz�����ࣨ������ÿ���ע�͵���
            services.AddSingleton<QuartzStartup>();
            services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
            services.AddSingleton<IJobFactory, IOCJobFactory>();
            //ע�� HttpClientHelp��������ÿ���ע�͵���
            services.AddTransient<HttpClientHelp>();

            //ע���Զ���job
            services.AddSingleton<TestJob>();

            services.Configure<ApiBehaviorOptions>(options =>
            {
                //����.net core webapi ��Ŀ�����ģ�Ͳ�������֤��ϵ
                options.SuppressModelStateInvalidFilter = true;
            });
            return RegisterAutofac(services);//ע��Autofac
        }

        #region ע��Autofac

        /// <summary>
        /// ע��Autofac
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private IServiceProvider RegisterAutofac(IServiceCollection services)
        {
            //ʵ����Autofac����
            var builder = new ContainerBuilder();
            //��Services�еķ�����䵽Autofac��
            builder.Populate(services);
            //��ģ�����ע��    
            builder.RegisterModule<AutofacModuleRegister>();
            //��������
            var Container = builder.Build();
            //������IOC�ӹ� core����DI���� 
            return new AutofacServiceProvider(Container);
        }

        #endregion

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        /// <param name="httpContextAccessor"></param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseHttpsRedirection();
            MyHttpContext.HttpContextAccessor = httpContextAccessor;
            //�������������ÿ���ע�͵���
            app.UseCors("EnableCrossDomain");
            app.UseStaticFiles(); //ע��wwwroot��̬�ļ���������ÿ���ע�͵���
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            //ִ�����ݵ��붨ʱͬ��Job
            //var quartz = app.ApplicationServices.GetRequiredService<QuartzStartup>();
            //��ÿ����ִ��һ�Ρ�
            //await quartz.Start<TestJob>("SyncTask", nameof(TestJob), "0 0/1 * * * ? ");
        }
    }
}
