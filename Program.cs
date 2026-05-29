using System;
using System.IO;
using System.Threading;

namespace Lab08
{
    class Program
    {
        static void Main()
        {
            double mu = 5.0;
            int n = 5;
            int totalRequests = 100;

            double[] p0exp_arr = new double[10];
            double[] lambdas = new double[10];
            double[] p0the_arr = new double[10];
            double[] pnexp_arr = new double[10];
            double[] pnthe_arr = new double[10];
            double[] qexp_arr = new double[10];
            double[] qthe_arr = new double[10];
            double[] aexp_arr = new double[10];
            double[] athe_arr = new double[10];
            double[] kexp_arr = new double[10];
            double[] kthe_arr = new double[10];

            Console.WriteLine("{0,-6} {1,-8} {2,-8} {3,-8} {4,-8} {5,-8} {6,-8} {7,-8} {8,-8} {9,-8}",
                "lambda", "P0the", "Pnexp", "Pnthe", "Qexp", "Qthe", "Aexp", "Athe", "Kexp", "Kthe");

            Random rng = new Random();

            for (int k = 1; k <= 10; k++)
            {
                double lambda = k * 5.0;

                Server server = new Server(mu, n);
                Client client = new Client(server);

                for (int id = 1; id <= totalRequests; id++)
                {
                    client.send(id);
                    int interval = (int)(-Math.Log(rng.NextDouble()) * 1000.0 / lambda);
                    Thread.Sleep(Math.Max(1, interval));
                }

                int maxServiceTime = (int)(5000.0 / mu);
                Thread.Sleep(maxServiceTime + 200);

                double pnexp = (double)server.rejectedCount / server.requestCount;
                double p0exp = (double)server.idleCount / server.requestCount;
                double qexp = (double)server.processedCount / server.requestCount;
                double aexp = lambda * qexp;
                double kexp = aexp / mu;

                double rho = lambda / mu;
                double sum = 0, factorial = 1;
                for (int i = 0; i <= n; i++)
                {
                    if (i > 0) factorial *= i;
                    sum += Math.Pow(rho, i) / factorial;
                }
                double p0the = 1.0 / sum;
                double factN = 1;
                for (int i = 1; i <= n; i++) factN *= i;
                double pnthe = (Math.Pow(rho, n) / factN) * p0the;
                double qthe = 1.0 - pnthe;
                double athe = lambda * qthe;
                double kthe = athe / mu;

                lambdas[k - 1] = lambda;
                p0exp_arr[k - 1] = p0exp;
                p0the_arr[k - 1] = p0the;
                pnexp_arr[k - 1] = pnexp;
                pnthe_arr[k - 1] = pnthe;
                qexp_arr[k - 1] = qexp;
                qthe_arr[k - 1] = qthe;
                aexp_arr[k - 1] = aexp;
                athe_arr[k - 1] = athe;
                kexp_arr[k - 1] = kexp;
                kthe_arr[k - 1] = kthe;

                Console.WriteLine("{0,-6:F1} {1,-8:F4} {2,-8:F4} {3,-8:F4} {4,-8:F4} {5,-8:F4} {6,-8:F4} {7,-8:F4} {8,-8:F4} {9,-8:F4}",
                    lambda, p0the, pnexp, pnthe, qexp, qthe, aexp, athe, kexp, kthe);
            }

            Directory.CreateDirectory("result");

            var plt1 = new ScottPlot.Plot(600, 400);
            plt1.AddScatter(lambdas, p0exp_arr, label: "P0 эксперимент");
            plt1.AddScatter(lambdas, p0the_arr, label: "P0 ожидаемое");
            plt1.Title("Вероятность простоя P0");
            plt1.XLabel("lambda"); plt1.YLabel("P0");
            plt1.Legend(); plt1.SaveFig("result/p-1.png");

            var plt2 = new ScottPlot.Plot(600, 400);
            plt2.AddScatter(lambdas, pnexp_arr, label: "Pn эксперимент");
            plt2.AddScatter(lambdas, pnthe_arr, label: "Pn ожидаемое");
            plt2.Title("Вероятность отказа Pn");
            plt2.XLabel("lambda"); plt2.YLabel("Pn");
            plt2.Legend(); plt2.SaveFig("result/p-2.png");

            var plt3 = new ScottPlot.Plot(600, 400);
            plt3.AddScatter(lambdas, qexp_arr, label: "Q эксперимент");
            plt3.AddScatter(lambdas, qthe_arr, label: "Q ожидаемое");
            plt3.Title("Относительная пропускная способность Q");
            plt3.XLabel("lambda"); plt3.YLabel("Q");
            plt3.Legend(); plt3.SaveFig("result/p-3.png");

            var plt4 = new ScottPlot.Plot(600, 400);
            plt4.AddScatter(lambdas, aexp_arr, label: "A эксперимент");
            plt4.AddScatter(lambdas, athe_arr, label: "A ожидаемое");
            plt4.Title("Абсолютная пропускная способность A");
            plt4.XLabel("lambda"); plt4.YLabel("A");
            plt4.Legend(); plt4.SaveFig("result/p-4.png");

            var plt5 = new ScottPlot.Plot(600, 400);
            plt5.AddScatter(lambdas, kexp_arr, label: "K эксперимент");
            plt5.AddScatter(lambdas, kthe_arr, label: "K ожидаемое");
            plt5.Title("Среднее число занятых каналов K");
            plt5.XLabel("lambda"); plt5.YLabel("K");
            plt5.Legend(); plt5.SaveFig("result/p-5.png");

            Console.WriteLine("Графики сохранены в папку result/");
        }
    }

    struct PoolRecord
    {
        public Thread thread;
        public bool in_use;
    }

    class Server
    {
        private PoolRecord[] pool;
        private object threadLock = new object();
        public int requestCount = 0;
        public int processedCount = 0;
        public int rejectedCount = 0;
        public int idleCount = 0;
        private double mu;
        private int n;

        public Server(double mu, int n)
        {
            this.mu = mu;
            this.n = n;
            pool = new PoolRecord[n];
        }

        public void proc(object sender, procEventArgs e)
        {
            lock (threadLock)
            {
                requestCount++;

                bool wasIdle = true;
                for (int i = 0; i < n; i++)
                    if (pool[i].in_use) { wasIdle = false; break; }
                if (wasIdle) idleCount++;

                for (int i = 0; i < n; i++)
                {
                    if (!pool[i].in_use)
                    {
                        pool[i].in_use = true;
                        pool[i].thread = new Thread(new ParameterizedThreadStart(Answer));
                        pool[i].thread.Start(e.id);
                        processedCount++;
                        return;
                    }
                }
                rejectedCount++;
            }
        }

        public void Answer(object arg)
        {
            Random rng = new Random();
            int actualService = (int)(-Math.Log(rng.NextDouble()) * 1000.0 / mu);
            Thread.Sleep(Math.Max(1, actualService));

            lock (threadLock)
            {
                for (int i = 0; i < n; i++)
                    if (pool[i].thread == Thread.CurrentThread)
                        pool[i].in_use = false;
            }
        }
    }

    class Client
    {
        private Server server;
        public Client(Server server)
        {
            this.server = server;
            this.request += server.proc;
        }
        public void send(int id)
        {
            procEventArgs args = new procEventArgs();
            args.id = id;
            OnProc(args);
        }
        protected virtual void OnProc(procEventArgs e)
        {
            EventHandler<procEventArgs> handler = request;
            if (handler != null) handler(this, e);
        }
        public event EventHandler<procEventArgs> request;
    }

    public class procEventArgs : EventArgs
    {
        public int id { get; set; }
    }
}
