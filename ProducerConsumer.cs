using Bogus;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BackendExam
{
    class ProducerConsumer
    {
        private Task m_FetcherTask;
        private Task m_DecoderTask;
        private Faker m_Faker = new Faker();
        private bool m_FastDataDecoder = true;
        private int _count = 0;
        private ConcurrentQueue<ReceivedData> DataQueue = new ConcurrentQueue<ReceivedData>();

        private SemaphoreSlim _semphore = new SemaphoreSlim(1);
        public ProducerConsumer(CancellationToken i_Token)
        {
            m_FetcherTask = Task.Run(async () =>
            {
                while (!i_Token.IsCancellationRequested)
                {
                    var dataToProcess = new ReceivedData(await GenerateSampleData());
                    Interlocked.Increment(ref _count);
                    DataQueue.Enqueue(dataToProcess);
                    _semphore.Release();
                }
            }, i_Token);

            m_DecoderTask = Decoder(i_Token);
        }

        public async Task<string> GenerateSampleData()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(m_Faker.Random.Int(10, 300))); //Simulate text generator
            return m_Faker.Lorem.Sentence(m_Faker.Random.Int(0, 100), m_Faker.Random.Int(0, 100));
        }

        private Task Decoder(CancellationToken i_Token)
        {
            return Task.Run(async () =>
            {
                try
                {
                    while (!i_Token.IsCancellationRequested)
                    {
                        await _semphore.WaitAsync(i_Token);
                        await ProcessConsumerData();
                    }
                }
                catch (OperationCanceledException)
                {
                    while(DataQueue.Count > 0 ) //process leftovers in queue at the time it was canceled,if has any.
                    {
                        await ProcessConsumerData();
                    }
                    Debug.Assert(_count == 0);
                }
            },i_Token);

        }
        private async Task ProcessConsumerData()
        {
            if (DataQueue.TryDequeue(out var dataConsumed))
            {
                await DecodeData(dataConsumed.Data);
                Interlocked.Decrement(ref _count);
            }
        }
        public async Task DecodeData(string i_Data)
        {
            await Task.Delay(GetDecoderDelay()); //Simulate text Processing
        }

        private TimeSpan GetDecoderDelay() =>
            TimeSpan.FromMilliseconds(m_FastDataDecoder ? m_Faker.Random.Int(0, 10) : m_Faker.Random.Int(10, 300));

        public Task WaitForCompletion() => Task.WhenAll(m_FetcherTask ?? Task.CompletedTask, m_DecoderTask ?? Task.CompletedTask);
    }
}
